---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Pipeline de Áudio

A parte do projeto onde mora a dificuldade real. Transcrever áudio é fácil;
transcrever áudio **enquanto ele acontece** é que é o problema.

## Captura

`WasapiLoopbackCapture` do NAudio, no dispositivo de renderização padrão. É o
caminho nativo do Windows para "o som que está indo para a saída" — sem driver
virtual, sem cabo virtual, sem gambiarra com ffmpeg.

Formato entregue: o do mixer, tipicamente 48 kHz / 2 canais / float 32-bit. O
pipeline converte para **mono 16 kHz**, que é o que o Whisper espera.

> [!danger] A regra inegociável do callback
> O handler de `DataAvailable` roda numa thread de áudio. Ele pode **apenas** copiar
> bytes e escrever num `Channel`. Nada de resample, log em disco, alocação grande ou
> await bloqueante ali dentro. Atrasar esse callback corta o áudio, e o sintoma
> (transcrição intermitente sem erro nenhum no log) é péssimo de diagnosticar.

Todo o processamento fica do outro lado do `Channel<AudioChunk>`, num consumidor
próprio. Ver [[Armadilhas Conhecidas]] para o comportamento do loopback em silêncio.

## Detecção de voz

Silero VAD via ONNX Runtime — modelo pequeno, roda em CPU sem pesar.

Preterido ao VAD por energia porque energia simples confunde música de fundo,
notificação do Teams e ruído de teclado com fala. Cada falso positivo vira uma
chamada de STT inútil, e no modo local isso é caro em GPU; no modo nuvem, em
dinheiro.

## Segmentação: janela deslizante

**Decisão fechada.** O segmentador reprocessa continuamente os últimos ~10 s de
áudio a cada ~800 ms, exibe o resultado como hipótese provisória, e **confirma** o
prefixo quando duas passagens consecutivas concordam (política LocalAgreement-2) ou
quando o VAD detecta pausa.

O motivo de existir essa complexidade toda: o Whisper **não é streaming**. Ele
transcreve blocos. Mandar blocos curtos para ter baixa latência produz transcrição
cortada no meio da frase, e tradução de frase cortada é lixo. Mandar blocos longos
produz boa qualidade com atraso inaceitável. A janela deslizante compra o melhor
dos dois ao custo de reprocessar áudio.

Esse custo tem um número: transcrever 10 s de áudio em menos de 800 ms é **~12x
tempo real**. É daí que sai [[Requisitos de Hardware]].

### A alternativa que foi avaliada e descartada

Segmentar por VAD e transcrever cada frase **uma vez só**. Muito mais barato, mas a
legenda aparece em bloco no fim de cada frase, com latência de 4–6 s em vez de
1,5–3 s. Fica como **modo de degradação** para máquina que não sustenta o RTF — o
app primeiro aumenta o intervalo de reprocessamento, e só depois cai para cá.
Degradar suave, nunca travar.

## Parâmetros configuráveis

Limiar de silêncio (~500–700 ms), duração mínima e máxima de segmento (teto de ~15 s
para não travar em quem fala sem pausar), intervalo de reprocessamento. Todos via
configuração: a calibração vai sair de ouvir chamadas reais, e recompilar a cada
ajuste é desperdício.

## Medições reais (18/08/2026)

Máquina de desenvolvimento, saída padrão "Alto-falantes (High Definition Audio
Device)":

| Medida | Valor |
|---|---|
| Formato entregue | 48.000 Hz, 2 canais, 32 bits, `Extensible` |
| Throughput | 384.000 bytes/s |
| Buffers por segundo | ~100 |
| Duração de cada buffer | **~10 ms** (3.840 bytes) |

> [!important] O orçamento de 10 ms
> É o tempo que o consumidor tem para processar um buffer antes do próximo chegar.
> Não é o limite do STT (esse trabalha sobre segmentos bem maiores) — é o limite da
> etapa de cópia, downmix e resample. Estourar isso produz `DataDiscontinuity`, que
> é o nome técnico do áudio perdido.

O flag `DataDiscontinuity` do `AudioBuffer` torna esse problema **detectável em
tempo de execução**, em vez de depender de alguém notar transcrição estranha. Vale
instrumentar isso na implementação de produção e não só no spike.

## Consumo: `IAsyncEnumerable`, não evento

`WasapiRecorder.CaptureAsync(CancellationToken)` devolve
`IAsyncEnumerable<AudioBuffer>`. Isso substitui com vantagem o padrão antigo de
evento `DataAvailable` + `Channel`: os buffers já chegam fora da thread de áudio do
driver, e o cancelamento é cooperativo.

A regra de não bloquear continua valendo — só mudou de lugar. O que não pode é o
corpo do `await foreach` demorar mais que o intervalo de chegada dos buffers.

## Resultado do spike completo (30 s, 18/08/2026)

| Medida | Valor |
|---|---|
| Buffers recebidos | 2.999 em 30 s |
| Tamanho do buffer | **3.840 bytes, fixo** — nunca variou |
| Duração por buffer | 10,0 ms exatos |
| Maior intervalo | 35 ms |
| Áudio gravado | 30,0 s, sem lacuna |
| `DataDiscontinuity` | 1 ocorrência |
| `Silent` | 0 ocorrências (ver ressalva abaixo) |

O buffer ter tamanho **fixo** simplifica o consumidor: dá para dimensionar estruturas
uma vez, sem realocar por chegada.

A única `DataDiscontinuity` aconteceu na transição de origem do áudio, não sob carga.
Serve como prova de que a instrumentação funciona — o flag realmente aparece quando
há perda, e é assim que a implementação de produção vai enxergar o problema.

> [!danger] O flag `Silent` é inútil para detectar silêncio
> Cinco segundos de silêncio digital absoluto (100% das amostras em zero, verificado
> por análise do WAV) produziram **zero** buffers marcados como `Silent`. A detecção
> tem que olhar o conteúdo. Ver [[Armadilhas Conhecidas]].

## O WASAPI aceita 16 kHz mono direto

`.WithFormat(new WaveFormat(16000, 16, 1))` foi aceito e o recorder passou a reportar
16.000 Hz mono — exatamente o que o Whisper consome.

> [!important] Isso remove uma etapa inteira do pipeline
> O desenho original previa capturar em 48 kHz estéreo e fazer downmix + resample por
> conta própria. Com o formato pedido direto na construção, essa etapa sai do código.

Duas ressalvas antes de considerar fechado:

1. A conversão não desaparece do mundo, apenas sai do nosso código — ela passa a
   acontecer dentro do NAudio ou do próprio endpoint do Windows. O ganho é de
   simplicidade e de superfície de bug, não necessariamente de CPU.
2. O teste provou que o formato é **aceito**, não que a qualidade da conversão serve.
   Validar com fala real antes de descartar o resample próprio: um resample ruim
   degrada a transcrição de um jeito que só aparece na taxa de erro do STT.

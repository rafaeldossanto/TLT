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

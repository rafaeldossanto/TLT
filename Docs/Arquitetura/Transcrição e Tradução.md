---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Transcrição e Tradução

São duas etapas separadas, e mantê-las separadas é uma decisão, não um acidente.

## STT: duas implementações, uma interface

`ISpeechRecognizer` tem implementação local e de nuvem, selecionáveis em runtime.

### Local (Whisper.net)

Pacotes `Whisper.net` + `Whisper.net.Runtime.Cuda` — sem o segundo, roda em CPU.

Manter o contexto vivo entre segmentos em vez de recarregar o modelo a cada chamada:
recarregar custa segundos e mata a latência.

Idioma de origem **fixo**, não auto-detectado. É mais rápido e mais confiável, e o
usuário já escolheu o idioma da chamada na interface de qualquer forma.

### Nuvem

Provider com streaming real e resultados parciais. Requisitos de implementação:

- Chave de API protegida com DPAPI (`ProtectedData`), nunca em texto plano no
  `appsettings.json`
- Reconexão com backoff
- **Teto de gasto configurável** — o app fica aberto por horas e a cobrança é por
  minuto de áudio

### Degradação

Se a nuvem cair no meio de uma chamada, o app avisa e cai para o local. O que não
pode acontecer é a legenda simplesmente parar sem explicação enquanto o usuário
está numa reunião.

## Tradução

`ITranslator` traduz **apenas segmentos confirmados**. Traduzir hipóteses
provisórias multiplica o custo e faz o texto dançar na tela, o que atrapalha
justamente a leitura que o produto existe para permitir.

Duas implementações, para casos diferentes:

| Implementação | Quando |
|---|---|
| API dedicada de tradução | Conversa comum. Mais barata, menor latência. |
| LLM | Reunião técnica. Recebe as últimas 3–5 frases como contexto, mantém terminologia e resolve pronomes. Mais cara e mais lenta. |

**Glossário configurável** de termos que não devem ser traduzidos: nomes de produto,
siglas internas, jargão da empresa. Parece detalhe menor, mas é o que separa legenda
utilizável de legenda irritante numa reunião de trabalho — nada destrói mais a
confiança do usuário do que ver o nome do próprio produto traduzido.

Cache de traduções idênticas: em chamada, saudações e confirmações curtas se repetem
muito.

## Regra de degradação (revisada em 18/08/2026)

Com a inversão para local por padrão (ver [[Privacidade por Padrão]]), a degradação
passa a ter direção obrigatória:

- **Nuvem falhando → cair para local**: permitido e automático. Reduz exposição.
- **Local insuficiente → subir para nuvem**: **proibido automaticamente**. Só com
  pergunta explícita ao usuário, porque aumenta exposição.

Quando o local não sustenta, degradar dentro dele primeiro: aumentar o intervalo de
reprocessamento, cair para segmentação por frase, sugerir modelo menor. A nuvem é a
última carta, e é uma pergunta — nunca um fallback silencioso.

## A tradução também precisa ser local

O texto transcrito **é** o conteúdo da conversa. Mandá-lo para uma API de tradução
enquanto se anuncia que "o áudio não sai da máquina" é uma meia-verdade.

Enquanto não houver tradução local, o modo privacidade está incompleto e não deve ser
vendido como completo. Ver [[Privacidade por Padrão]].

## Estado da implementação (18/08/2026)

`Tlt.Stt.Local.WhisperSpeechRecognizer` implementa `ISpeechRecognizer`. Verificado
ponta a ponta com o pipeline completo — áudio, VAD, segmentação, transcrição — sobre
57 s de fala: backend **Vulkan** confirmado em execução, oito trechos transcritos com
~985 ms de média cada e o texto saindo correto.

Três coisas que a implementação carrega:

- `RuntimeOptions.RuntimeLibraryOrder` configurado antes de carregar o modelo, senão
  o Whisper.net usa CPU mesmo com as DLLs de GPU no lugar
- `Backend` exposto a partir de `RuntimeOptions.LoadedLibrary`, porque a queda para
  CPU é silenciosa
- `WarmUpAsync` com ruído de 1 s descartado, para a compilação de shaders do Vulkan
  não cair sobre a primeira frase do usuário

> [!warning] `IsConfirmed` sai como falso, sempre
> O reconhecedor não sabe se aquele áudio ainda pode ser revisado — isso é decisão da
> política de streaming, que vive uma camada acima. Marcar como confirmado aqui faria
> texto provisório seguir para a tradução e para a tela como se fosse definitivo.

## O que ainda falta: a janela deslizante

O que existe hoje entrega **segmentação por frase**: o VAD fecha o trecho, o
reconhecedor transcreve. Funciona, e é o modo de degradação previsto no ADR — mas
não é a arquitetura escolhida.

A latência desse modo é *duração da frase* mais ~1 s. No teste real houve uma frase de
11,24 s, o que daria mais de 12 segundos até a legenda aparecer. É utilizável para
acompanhar, mas está longe do alvo de 1,5–3 s.

Falta a camada que reprocessa o trecho **em curso** a cada 1,5 s, emite hipótese
provisória e confirma o prefixo quando duas passagens concordam (LocalAgreement-2).
Ela consome o mesmo `ISpeechRecognizer` — nada da implementação atual precisa mudar.

## Provedor de nuvem: não implementado

Depende de duas decisões que não são técnicas: **qual serviço** e **com qual conta**.
A interface e a regra de degradação já estão definidas; falta escolher o fornecedor,
comparar preço por minuto e ter credencial para testar.

Como o ADR fixou local como padrão e proibiu subida automática para nuvem, a ausência
desse provedor não bloqueia o produto — ela só limita o atendimento a máquinas sem
GPU capaz.

## A janela deslizante existe (18/08/2026)

`Tlt.Core.SlidingWindowTranscriber` implementa LocalAgreement-2: o prefixo em que
**duas passagens consecutivas concordam** é dado por estável e confirmado; o resto
segue como hipótese revisável, que a interface mostra em cinza.

Fica no núcleo, como a política de segmentação, porque é decisão de produto — e assim
é testável com um reconhecedor falso que devolve transcrições programadas.

### Medido em ritmo de tempo real

Áudio entregue a 100 ms por bloco com espera de 100 ms entre eles, como numa chamada.
A latência abaixo é a diferença entre o instante em que a palavra foi dita e o
instante em que apareceu confirmada:

| | Valor |
|---|---|
| Confirmações em 57 s | 31 |
| Latência média | **927 ms** |
| Mediana | 919 ms |
| Máxima | 1.437 ms |

> [!success] Abaixo do alvo
> O ADR pedia 1,5–3 s. A implementação entrega menos de 1 s na mediana, com o texto
> saindo em pedaços de 3 a 6 palavras a cada 1,5 s — cadência natural para legenda ao
> vivo.

### O erro que a medição pegou

A primeira versão descartava só o excedente do buffer quando ele passava de 10 s,
mantendo a fala em curso. Parecia certo, e estava errado: descartar o início **quebra
a cadeia do LocalAgreement**, porque a passada seguinte não tem com o que comparar.

O sintoma era claro no log em tempo real — trechos longos passavam quinze segundos só
com hipóteses, e aí despejavam uma confirmação enorme de uma vez.

A correção foi tratar o estouro da janela como fim de trecho: confirma tudo e
recomeça. Custa perder contexto entre trechos, e em troca as confirmações passaram de
11 para 31 no mesmo áudio, com a latência caindo de 1.102 ms para 927 ms.

> [!tip] Nenhum teste de unidade pegaria isso
> Os testes com reconhecedor falso passavam nas duas versões. O defeito só apareceu
> alimentando áudio real em ritmo real e olhando **quando** cada confirmação saía.

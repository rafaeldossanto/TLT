---
tags: [decisao, medicao]
atualizado: 2026-08-18
---

# Conversão de Áudio

Decisão de 18/08/2026, fechando a dúvida aberta pelo spike de captura: o pipeline
converte o áudio por conta própria, ou pede o formato pronto ao WASAPI?

## Decisão: conversão própria

`AudioCaptureOptions.RequestFormatFromDevice` fica em **falso**. O pipeline captura no
formato nativo do dispositivo e converte para 16 kHz mono com `WdlResampler`.

## A medição

Mesma fala tocada duas vezes e capturada por cada caminho, transcrita pelo mesmo
modelo, com a taxa de erro medida contra o texto de referência:

| Caminho | Formato bruto | WER |
|---|---|---|
| **Conversão própria** | 48.000 Hz / 2 ch | **29,3%** |
| Formato pedido ao WASAPI | 16.000 Hz / 1 ch | 41,4% |

**Doze pontos percentuais** separam os dois. Pedir o formato pronto entregaria menos
código e transcrição sensivelmente pior — o resample interno do Windows não preserva a
qualidade que o reconhecedor precisa.

> [!important] Aceitar não é o mesmo que converter bem
> O spike de captura tinha mostrado que o WASAPI **aceita** o pedido de 16 kHz mono, e
> era tentador concluir dali que a etapa de conversão podia sair do código. Aquele
> teste usou tom senoidal, o sinal mais fácil que existe para reamostrar. Com fala, a
> diferença apareceu.

## Ressalva sobre os números absolutos

Os dois WER estão altos. Transcrevendo o mesmo arquivo **diretamente**, sem passar por
reprodução e captura, o WER fica em 5,7%.

A diferença não vem do pipeline: vem da bancada de teste. O áudio de referência é de
16 kHz, o Windows o converte para os 48 kHz do mixer para tocar, e só então ele é
capturado e convertido de volta. São duas conversões extras que o uso real não tem —
numa chamada, o áudio chega ao mixer na qualidade em que foi transmitido.

Ou seja: **29,3% não é a qualidade esperada do produto**, é o custo de tocar e
recapturar. O que a medição estabelece com segurança é a **comparação** entre os dois
caminhos, já que ambos atravessaram exatamente a mesma bancada.

## Consequência

A etapa de conversão fica no código, com o filtro anti-aliasing que já está coberto
por teste em [[Pipeline de Áudio]]. A opção de pedir o formato ao dispositivo continua
existindo na configuração, para quem quiser trocar qualidade por um passo a menos.

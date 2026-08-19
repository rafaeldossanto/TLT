---
tags: [produto]
atualizado: 2026-08-18
---

# Requisitos de Hardware

Só se aplica ao **modo local**. No modo nuvem o processamento sai da máquina e
qualquer PC capaz de sustentar uma chamada de vídeo atende.

## O número que manda

A janela deslizante reprocessa ~10 s de áudio a cada ~800 ms. Transcrever 10 s em
menos de 800 ms é **RTF ≥ 12** (real-time factor). Abaixo disso o app degrada:
primeiro aumenta o intervalo de reprocessamento, depois cai para segmentação por
frase, e por último recomenda o modo nuvem.

## Medições reais

Máquina de desenvolvimento — **GTX 1050 Ti** (Pascal, 4 GB, 2016), i5-7400, via
**Vulkan**. Áudio de 57 s, quantização Q5_0, idioma fixo em inglês.

| Modelo | Tamanho | Tempo | RTF | WER | Veredito |
|---|---|---|---|---|---|
| tiny | 28 MB | 1,4 s | **40,2x** | 8,3% | folga enorme, qualidade pior |
| base | 53 MB | 2,0 s | **29,3x** | 6,4% | folga grande |
| **small** | 167 MB | 4,2 s | **13,7x** | 5,7% | **ponto ótimo** |
| medium | 514 MB | 8,4 s | 6,8x | 5,7% | só segmentação por frase |
| large-v3-turbo | 547 MB | 8,2 s | 7,0x | 5,7% | só segmentação por frase |

> [!success] A janela deslizante é viável numa GPU de 2016
> Era a dúvida que travava a arquitetura. `small` entrega RTF 13,7x numa 1050 Ti —
> acima do alvo de 12. O desenho de baixa latência **não** exige hardware caro.

Em CPU pura (i5-7400, 4 núcleos), para comparação: `base` fica em 6,0x e `small` em
1,8x. Ou seja, **sem GPU o modo local não sustenta a janela deslizante**, e a partir
de `small` mal acompanha o tempo real.

## Escolha padrão: `small`

Ganha por eliminação. Tem o mesmo WER de `medium` e `turbo` neste teste, com o dobro
da velocidade e um terço do tamanho. `base` seria mais rápido, mas erra mais.

> [!warning] Duas ressalvas antes de tratar isto como definitivo
> **O áudio de teste era TTS**, o material mais fácil que existe para transcrever —
> sem sotaque, sem ruído, sem sobreposição de falas. Em chamada real os WER sobem, e
> é provável que `medium` e `turbo` se separem de `small`, o que aqui não aconteceu.
> Refazer a comparação com gravação de reunião de verdade.
>
> **A GPU não estará livre.** Estes números são de uma máquina ociosa. Durante uma
> chamada, o app de vídeo está decodificando na mesma GPU. A folga real de `small`
> (13,7 contra 12) é pequena para esse cenário — medir sob carga antes de fixar o
> padrão.

## Distribuição

O runtime Vulkan do Whisper.net ocupa **58 MB** para Windows x64 (`ggml-vulkan`
responde por 56 MB). É o que precisa ir no instalador, além do modelo escolhido.

`small` em Q5_0 são mais 167 MB, baixados sob demanda na primeira execução em vez de
embutidos — instalador menor e permite trocar de modelo sem reinstalar.

## Revisão do critério (18/08/2026)

A tabela acima usa RTF medido sobre 57 s de áudio, o que **superestima** o que a
janela deslizante consegue: buffer longo amortiza o overhead fixo de ~820 ms por
chamada, e a janela trabalha com 10 s.

O critério correto para o modo de baixa latência é o custo absoluto por passada, não
o RTF. Na GTX 1050 Ti com `small`, uma passada de 10 s custa 1.083 ms, o que sustenta
reprocessamento a cada 1,5 s com 72% de ocupação. Ver [[Custo da Transcrição]].

Ao medir os tiers em GPU alugada (ainda pendente), medir **o custo de uma passada de
10 s**, e não o RTF sobre um arquivo longo.

---
tags: [produto]
atualizado: 2026-08-17
---

# Requisitos de Hardware

Só se aplica ao **modo local**. No modo nuvem o processamento sai da máquina e
qualquer PC capaz de rodar uma chamada de vídeo atende.

## O número que manda

A janela deslizante reprocessa ~10 s de áudio a cada ~800 ms. Transcrever 10 s em
menos de 800 ms é **~12x tempo real (RTF ≥ 12)**.

Esse é o alvo. Abaixo dele o app degrada: primeiro aumenta o intervalo de
reprocessamento, depois cai para segmentação por frase, e por último recomenda o
modo nuvem.

## Tiers

> [!todo] A medir
> A tabela abaixo é a saída da task #3 e ainda **não tem dados reais**. Não publicar
> requisito de sistema com estimativa: prometer de menos perde cliente, prometer
> demais gera reembolso.

| Tier | GPU | Modelo | Latência esperada |
|---|---|---|---|
| Alto | a medir | `large-v3-turbo` | 1,5–3 s |
| Médio | a medir | `medium` ou `small` | a medir |
| Baixo | a medir | `small` | modo degradado |
| Sem GPU | — | — | só nuvem |

## Como medir sem ter o hardware

Alugar GPU por hora na RunPod ou Vast.ai. Uma RTX 3060, uma 4070 e uma 4090 por uma
hora cada custa poucos dólares e produz número real em vez de estimativa.

A máquina de desenvolvimento atual (GTX 1050 Ti, 4 GB, Pascal sem Tensor Cores) serve
para medir o **piso**, não o alvo — decisão explícita de 17/08/2026 de não
dimensionar o produto por ela.

## Metodologia

Áudio de referência de ~60 s capturado do próprio loopback, de call ou vídeo técnico
real. **Não usar áudio de estúdio limpo**: ele infla a qualidade aparente e esconde
exatamente os erros que aparecem no uso real. Registrar RTF, VRAM ocupada e se o
modelo acertou nomes próprios, siglas e jargão.

Medir também CPU-only, para saber a partir de onde apenas a nuvem atende.

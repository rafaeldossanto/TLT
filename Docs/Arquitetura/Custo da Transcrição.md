---
tags: [arquitetura, medicao]
atualizado: 2026-08-18
---

# Custo da Transcrição

Medido em 18/08/2026 na GTX 1050 Ti, `small` Q5_0 via Vulkan, depois do aquecimento.

| Áudio | Tempo | RTF |
|---|---|---|
| 1 s | 824 ms | 1,2x |
| 2 s | 837 ms | 2,4x |
| 5 s | 964 ms | 5,2x |
| 10 s | 1.083 ms | 9,2x |
| 20 s | 1.497 ms | 13,4x |
| 30 s | 2.475 ms | 12,1x |
| 40 s | 2.930 ms | 13,7x |

## O custo é quase todo fixo

Transcrever 1 segundo custa 824 ms; transcrever 10 custa 1.083 ms. **Dez vezes mais
áudio por 31% a mais de tempo.** O piso de ~820 ms por chamada domina o resultado.

A causa é a arquitetura do Whisper: ele processa em janelas fixas de 30 segundos,
preenchendo com silêncio o que falta. Um buffer de 2 segundos vira internamente uma
janela de 30. Por isso o salto entre 30 s e 40 s — aí entra a segunda janela.

> [!important] RTF é a métrica errada para decidir o intervalo
> O RTF varia de 1,2x a 13,7x na mesma máquina e no mesmo modelo, só mudando o
> tamanho do buffer. Ele é um resultado, não um parâmetro. O que decide a viabilidade
> é o **custo absoluto por passada** contra o intervalo de reprocessamento.

## O que isso corrige no ADR

O alvo registrado era "RTF ≥ 12, porque a janela deslizante reprocessa 10 s a cada
800 ms". Os 13,7x medidos na task #3 vinham de um buffer de 57 s, que amortiza o
overhead — em janela de 10 s o RTF efetivo é 9,2x.

Refazendo a conta com o número certo: uma passada de 10 s custa **1.083 ms**.

| Intervalo | Ocupação | Veredito |
|---|---|---|
| 800 ms | 135% | **inviável** — não termina antes da próxima |
| 1.200 ms | 90% | sem folga para o resto do sistema |
| **1.500 ms** | **72%** | **viável** |
| 2.000 ms | 54% | confortável |

Com intervalo de 1,5 s, a hipótese aparece entre **1,1 e 2,6 segundos** depois da
fala — dentro do alvo de 1,5–3 s. A arquitetura continua de pé; o que muda é o
parâmetro, de 800 ms para 1,5 s.

## Consequências de desenho

**Trecho curto é caro por natureza.** Transcrever 1 s custa quase o mesmo que
transcrever 10 s. Filtrar tosse e sílaba solta no VAD, antes de chegar ao
reconhecedor, economiza quase uma chamada inteira — o
[[Pipeline de Áudio]] já descarta trechos abaixo de 300 ms.

**Encolher a janela quase não ajuda.** De 10 s para 5 s economiza 119 ms, 11% do
custo, e em troca o modelo perde metade do contexto. Não compensa.

**Ampliar a janela é barato.** De 10 s para 20 s custa 38% a mais e dobra o contexto
disponível. Vale medir se a qualidade da transcrição melhora o suficiente para
justificar.

**A segmentação por frase tem um teto ruim.** Latência dela é *duração da frase* mais
~1 s. Numa frase de 11 s — que aconteceu no teste real — isso dá mais de 12 segundos
até a legenda aparecer. Confirma a janela deslizante como necessária, e não como
refinamento.

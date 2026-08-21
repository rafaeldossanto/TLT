---
tags: [arquitetura, medicao]
atualizado: 2026-08-18
---

# Disputa de Recursos

Medido em 18/08/2026 na máquina de desenvolvimento (GTX 1050 Ti, i5-7400 de 4 núcleos
sem hyperthreading). Todas as medições anteriores do projeto foram feitas com a
máquina ociosa, o que não é o cenário de uso.

## O gargalo compartilhado é CPU, não GPU

Custo de uma passada de 10 s do reconhecedor, que precisa caber no intervalo de
reprocessamento de 1,5 s:

| Cenário | Antes do ajuste | Depois |
|---|---|---|
| Máquina ociosa | 1.214 ms (81%) | 1.153 ms (77%) |
| **Pipeline completo** | **2.811 ms (187%)** | **1.331 ms (89%)** |
| GPU disputada por outro reconhecedor | 1.993 ms (133%) | 1.848 ms (123%) |

> [!danger] O pipeline completo não cabia
> Com o tradutor usando todos os núcleos, o custo do reconhecedor **mais que dobrou** e
> passou de 187% do intervalo. Na prática isso significa a passada seguinte começando
> antes de a anterior terminar — atraso que se acumula durante a reunião inteira.

A causa não é a GPU. O reconhecedor roda em Vulkan, mas **depende de CPU para alimentar
a GPU**, e o tradutor rodando em ONNX Runtime ocupava os quatro núcleos. Era um
problema de CPU disfarçado de problema de GPU.

## A correção: limitar os núcleos da tradução

`OpusMtOptions.MaxThreads = 2`, aplicado via `SessionOptions.IntraOpNumThreads`.

O efeito no reconhecedor era o esperado: degradação caiu de **+131% para +15%**.

O efeito na tradução **não** era:

| Núcleos da tradução | Latência |
|---|---|
| padrão (todos) | 601 ms |
| 1 | 258 ms |
| **2** | **245 ms** |
| 3 | 322 ms |
| 4 | 539 ms |

> [!success] Menos núcleos deixaram a tradução mais rápida
> Não é trade-off: é ganho dos dois lados. O modelo é pequeno e as operações são
> curtas, então coordenar quatro threads custa mais do que rende. Espalhar trabalho
> pequeno por muitos núcleos é uma piora que costuma passar por otimização.

## O que não foi medido

> [!warning] A carga de uma videochamada real não foi reproduzida
> Uma chamada usa o **decodificador de vídeo dedicado** da GPU — bloco separado das
> unidades de compute que o Whisper utiliza —, mais composição de janelas e a captura
> da webcam. O cenário "GPU disputada" da tabela usa um segundo reconhecedor, que
> satura o compute de um jeito que uma chamada provavelmente não satura.
>
> O número de 123% naquele cenário deve ser lido como **pior caso artificial**, não
> como previsão para o Teams rodando ao lado.

Para fechar essa lacuna, o teste é direto: abrir uma chamada real com câmera e
compartilhamento de tela ativos e repetir a medição do custo por passada.

## Consequência para outras máquinas

O valor de 2 núcleos foi calibrado num processador de 4 núcleos sem hyperthreading.
Numa máquina com 8 ou 16, reservar apenas 2 para a tradução provavelmente desperdiça
capacidade, e o ponto ótimo será outro.

Isso é candidato natural a **calibração na primeira execução**: medir uma passada com
alguns valores e fixar o melhor, em vez de embutir um número que só vale para o
hardware onde foi medido.

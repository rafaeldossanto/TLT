---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Interface e Overlay

Janela WPF que fica sobre a chamada de vídeo. Tecnicamente simples, mas com dois
detalhes que definem se o produto é usável.

## Configuração da janela

`WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, fora da barra de
tarefas. Arrastável pela área de texto, com posição e tamanho persistidos entre
sessões.

Fundo semitransparente escuro com contraste alto: o overlay vai ficar sobre vídeo de
conteúdo imprevisível, e legenda que some sobre fundo claro não serve para nada.

Atalho global para mostrar e esconder **sem tirar o foco da chamada**.

## As duas linhas

Original e tradução, com a original ocultável nas preferências — parte dos usuários
só quer ler a tradução.

> [!important] Confirmado em branco, provisório em cinza
> Essa distinção visual não é enfeite. A janela deslizante **corrige** o texto
> enquanto ele é refinado; sem o sinal de que aquilo ainda pode mudar, o usuário lê
> uma frase, ela se altera sozinha, e ele perde a confiança na legenda inteira.

## A armadilha do compartilhamento de tela

Se o usuário compartilhar a tela na reunião, o overlay aparece para todos os
participantes — incluindo a tradução do que eles acabaram de dizer.

Solução: `SetWindowDisplayAffinity` com `WDA_EXCLUDEFROMCAPTURE`, oferecido como
opção. Sem isso, a primeira vez que alguém compartilhar tela usando o TLT vai ser
constrangedora, e é o tipo de coisa que vira review ruim.

## Separação

A UI apenas consome eventos do pipeline. Se o overlay souber o que é VAD ou janela
deslizante, o desenho em camadas de [[Visão Geral]] foi perdido.

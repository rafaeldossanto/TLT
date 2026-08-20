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

## Implementação (18/08/2026)

`Tlt.App.Overlay.OverlayWindow`. A janela é `WindowStyle=None`,
`AllowsTransparency=True`, `Topmost`, fora da barra de tarefas, com fundo preto a 78%
de opacidade e cantos arredondados.

### Três linhas

| Linha | Cor | Conteúdo |
|---|---|---|
| status | cinza escuro, 11pt | dispositivo, modelo e **backend ativo** |
| confirmado | branco, 21pt | texto que não muda mais |
| provisório | cinza, 21pt | hipótese ainda revisável |

A linha de status existe por causa da queda silenciosa para CPU: sem ela, o usuário
que reclamar de lentidão não tem como dizer qual biblioteca está rodando.

### Escondida de compartilhamento de tela

`SetWindowDisplayAffinity` com `WDA_EXCLUDEFROMCAPTURE`, ligado por padrão.

> [!important] A falha é avisada, não engolida
> Se a chamada não funcionar, o status mostra o aviso. Falhar em silêncio deixaria o
> usuário compartilhar a tela achando que a legenda está escondida — pior que não ter
> o recurso.

### Detalhes que evitam suporte depois

- **Arrasto pelo corpo**, já que não há barra de título; posição e largura persistidas
  em `%APPDATA%\TLT\overlay.json`
- **Posição validada contra os monitores atuais**: se a preferência aponta para um
  monitor desconectado desde a última sessão, a janela volta para o rodapé do
  principal. Sem isso ela abriria fora da tela, invisível, sem como recuperar a não
  ser apagando o arquivo na mão
- **Preferência corrompida não impede o app de abrir**: perder a posição da janela
  custa muito menos que não subir
- **Ctrl+Alt+L** mostra e esconde sem tirar o foco da chamada
- **Últimas 40 palavras** na tela: numa reunião de uma hora o texto confirmado
  cresceria sem parar, e a legenda serve para acompanhar o agora, não para ler
  histórico

### O pipeline roda fora da thread de interface

Carregar modelos e capturar áudio são operações longas. `TranscriptionService` roda
solto e atualiza a janela pelo `Dispatcher`; erro no pipeline vira texto na linha de
status, em vez de deixar o overlay parado sem explicação no meio de uma reunião.

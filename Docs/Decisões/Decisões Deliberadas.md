---
tags: [decisao]
atualizado: 2026-08-17
---

# Decisões Deliberadas

Escolhas feitas de propósito, com o motivo registrado. Não "corrigir" sem reler o
porquê.

## Não é Ruby on Rails

Foi a primeira ideia, e foi descartada. São dois problemas distintos:

**Rails** é um framework web MVC que roda no servidor e devolve HTML/JSON por HTTP.
Não é runtime de aplicação desktop. Empacotar um servidor Rails com um navegador
embutido para desenhar uma janela de legenda paga o peso de um servidor web inteiro
sem receber nada que resolva o problema real, que é falar com a placa de som.

**Ruby**, como linguagem, também não serve aqui: não tem binding maduro para WASAPI
loopback (a saída seria extensão em C ou shell-out para ffmpeg, frágil a cada
atualização de driver), tem GIL num pipeline que trabalha com buffers de 10–20 ms, e
não tem ecossistema de STT.

Onde Rails caberia de verdade: um backend futuro de contas, licenças e cobrança. Mas
para isso já existe Spring Boot no repertório, sem custo de aprendizado.

## É C# / .NET 8 + WPF

- `NAudio.WasapiLoopbackCapture` resolve nativamente a parte mais difícil
- Sintaxe próxima de Java: a curva é de biblioteca, não de linguagem
- Overlay always-on-top é trivial em WPF
- Distribuição limpa: exe self-contained ou MSIX
- .NET 8 por ser LTS — suporte estendido importa em produto que será vendido

**Avaliadas e descartadas:** Tauri (melhor produto final, mas Rust atrasa a v1 em
semanas — volta à mesa se cross-platform virar requisito de negócio) e Electron
(~150 MB de bundle e, no macOS, ainda exige driver virtual: paga-se o custo do
cross-platform sem receber o benefício).

## IDE é Rider

Mesma UX do IntelliJ já em uso — zero curva de ferramenta enquanto se aprende C# e
WPF ao mesmo tempo. Exige assinatura individual, já que o uso é comercial e a
licença gratuita do Rider cobre apenas uso não-comercial.

VS Community seria a alternativa gratuita, e tem melhor suporte a XAML. Mas a
vantagem dele é o designer visual, que **não ajuda** num overlay transparente sem
borda — esse XAML se escreve à mão nos dois IDEs, e a verificação é rodando o app.

## Janela deslizante, não segmentação por frase

Ver [[Pipeline de Áudio]] para o mecanismo. Resumo: 1,5–3 s de latência contra
4–6 s. A segmentação por frase sobrevive como modo de degradação.

## Nuvem como padrão de fábrica, local como modo privacidade

Contraintuitivo à primeira vista, já que o modo local é o diferencial comercial. Mas
o padrão precisa **funcionar** na máquina de quem instala, e o modo local exige
hardware que nem todo cliente tem. O local é oferecido com destaque e é o argumento
de venda; só não é o que roda antes do usuário escolher. Ver
[[Requisitos de Hardware]].

## Transcrição e tradução são etapas separadas

O Whisper tem uma tarefa `translate` embutida, e é tentador economizar uma etapa com
ela. Não serve: ela só traduz **para inglês**. Ver [[Armadilhas Conhecidas]].

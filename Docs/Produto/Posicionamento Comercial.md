---
tags: [produto]
atualizado: 2026-08-17
---

# Posicionamento Comercial

Uso pessoal **e** comercial desde a concepção. Isso muda decisões técnicas, e é por
isso que esta nota existe num cofre de arquitetura.

## O diferencial não pode ser "traduz chamada"

Teams e Meet já têm legenda traduzida ao vivo nativa, de graça, para quem usa essas
plataformas. Competir no genérico é competir contra recurso embutido.

Os dois eixos que sobram, e ambos importam:

1. **Agnóstico de plataforma.** Funciona em qualquer aplicativo — inclusive vídeo,
   áudio, cursos e ferramentas que não têm legenda própria. Uma plataforma legenda a
   si mesma; o TLT legenda o sistema.
2. **Roda local, o áudio não sai da máquina.** Esse é o eixo forte no mercado
   corporativo.

## Por que o modo local é a alavanca de venda

Quando um app processa áudio de reunião de trabalho, o comprador corporativo vai
perguntar para onde vai esse áudio. É a primeira pergunta, sempre.

Com processamento em nuvem, a resposta envolve subprocessador, contrato e política
de privacidade — e o áudio contém fala de **terceiros** que não instalaram nada nem
consentiram com nada, o que puxa LGPD para dentro da negociação.

Com o modo local, a resposta é "não sai da máquina". Isso converte o maior passivo do
produto no seu maior argumento. Por isso o local é requisito de v1 e não recurso
futuro, mesmo custando a complexidade toda de [[Requisitos de Hardware]].

## Notas legais

Gravar conversa da qual se participa é lícito no Brasil. O que exige cuidado é o
produto vendido que **transporta** áudio de terceiros para uma API: aí entram
política de privacidade, base legal e transparência sobre o subprocessador.

O modo local não elimina a obrigação de ter política de privacidade, mas remove o
transporte — que é a parte difícil de justificar.

> [!warning] Custo no modo nuvem
> A cobrança é por minuto de áudio e o app fica **aberto por horas**. Um usuário
> pesado pode custar mais que a própria assinatura. O teto de gasto configurável não
> é conveniência, é proteção de margem. Verificar preços atuais antes de precificar.

## A promessa só fecha com tradução local

Decidido em 18/08/2026 que a transcrição roda local por padrão
(ver [[Privacidade por Padrão]]). Isso transforma privacidade de argumento em
característica — mas só se a **tradução** também for local.

Com STT local e tradução em nuvem, a frase honesta seria "o áudio não sai da sua
máquina, o texto sai". Nenhum comprador corporativo aceita essa distinção, e nem
deveria: o texto é a conversa.

O que torna a promessa demonstrável, e não apenas afirmada: **o app funciona com a
rede desligada**. O cliente desconfiado testa em trinta segundos. É o tipo de prova
que vale mais que qualquer página de política de privacidade.

---
tags: [moc, tlt]
atualizado: 2026-08-17
---

# TLT

Sigla de **Transleitor**.

Tradutor de fala **em tempo real** para desktop. O app fica aberto em segundo plano,
captura o áudio que o PC está enviando para o fone ou caixa de som, transcreve e
traduz para o idioma escolhido, exibindo o resultado num **overlay** por cima de
qualquer janela.

> [!example] Caso de uso que originou o projeto
> Chamada de vídeo de trabalho com alguém que só fala inglês. O TLT fica aberto,
> a chamada acontece normalmente na ferramenta de sempre (Teams, Meet, Zoom — o
> app é agnóstico), e a legenda traduzida aparece sobreposta enquanto a pessoa fala.

A diferença para as legendas nativas do Teams ou do Meet: o TLT funciona em
**qualquer** aplicativo, inclusive vídeo, áudio e ferramentas que não têm legenda
própria — e pode rodar **100% local**, sem o áudio sair da máquina. Ver
[[Posicionamento Comercial]].

> [!info] Estado: fundação pronta, sem funcionalidade
> A solution existe, compila e tem testes. O que existe são as **abstrações** de
> `Tlt.Core` e o esqueleto dos projetos — nenhuma implementação real ainda: não
> captura, não transcreve, não traduz. As três medições que sustentam o desenho já
> foram feitas e estão registradas com números, não com suposição.
>
> As notas de arquitetura ainda descrevem o alvo mais do que o construído. A
> [[Linha do Tempo]] marca o que já saiu do papel.

## Arquitetura

- [[Visão Geral]] — os projetos da solution e por que a divisão é essa
- [[Pipeline de Áudio]] — captura WASAPI, resample, VAD e segmentação
- [[Transcrição e Tradução]] — STT local e nuvem, tradução com contexto
- [[Interface e Overlay]] — a janela de legenda e suas armadilhas

## Produto

- [[Requisitos de Hardware]] — tiers de GPU e o que cada um entrega
- [[Posicionamento Comercial]] — diferencial, concorrência e LGPD

## Contexto

- [[Privacidade por Padrão]] — o ADR: transcrição local por padrão e o que decorre disso
- [[Tradução Local]] — a investigação que falta para a promessa de privacidade fechar
- [[Decisões Deliberadas]] — o que foi escolhido e o que foi descartado, com o porquê
- [[Armadilhas Conhecidas]] — erros mapeados antes de acontecer
- [[Linha do Tempo]] — histórico do projeto
- [[Glossário]] — RTF, VAD, LocalAgreement e o resto do vocabulário

## Stack

| Camada | Escolha |
|---|---|
| Runtime | .NET 10 (LTS) |
| Linguagem | C# |
| UI | WPF (Windows) |
| Áudio | NAudio — `WasapiLoopbackCapture` |
| STT local | Whisper.net (whisper.cpp) |
| STT nuvem | provider com streaming e resultados parciais |
| VAD | Silero VAD via ONNX Runtime |
| IDE | Rider |

O porquê de cada uma dessas está em [[Decisões Deliberadas]] — inclusive o porquê
de **não** ser Ruby on Rails, que foi a primeira ideia.

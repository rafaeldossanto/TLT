---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Transcrição e Tradução

São duas etapas separadas, e mantê-las separadas é uma decisão, não um acidente.

## STT: duas implementações, uma interface

`ISpeechRecognizer` tem implementação local e de nuvem, selecionáveis em runtime.

### Local (Whisper.net)

Pacotes `Whisper.net` + `Whisper.net.Runtime.Cuda` — sem o segundo, roda em CPU.

Manter o contexto vivo entre segmentos em vez de recarregar o modelo a cada chamada:
recarregar custa segundos e mata a latência.

Idioma de origem **fixo**, não auto-detectado. É mais rápido e mais confiável, e o
usuário já escolheu o idioma da chamada na interface de qualquer forma.

### Nuvem

Provider com streaming real e resultados parciais. Requisitos de implementação:

- Chave de API protegida com DPAPI (`ProtectedData`), nunca em texto plano no
  `appsettings.json`
- Reconexão com backoff
- **Teto de gasto configurável** — o app fica aberto por horas e a cobrança é por
  minuto de áudio

### Degradação

Se a nuvem cair no meio de uma chamada, o app avisa e cai para o local. O que não
pode acontecer é a legenda simplesmente parar sem explicação enquanto o usuário
está numa reunião.

## Tradução

`ITranslator` traduz **apenas segmentos confirmados**. Traduzir hipóteses
provisórias multiplica o custo e faz o texto dançar na tela, o que atrapalha
justamente a leitura que o produto existe para permitir.

Duas implementações, para casos diferentes:

| Implementação | Quando |
|---|---|
| API dedicada de tradução | Conversa comum. Mais barata, menor latência. |
| LLM | Reunião técnica. Recebe as últimas 3–5 frases como contexto, mantém terminologia e resolve pronomes. Mais cara e mais lenta. |

**Glossário configurável** de termos que não devem ser traduzidos: nomes de produto,
siglas internas, jargão da empresa. Parece detalhe menor, mas é o que separa legenda
utilizável de legenda irritante numa reunião de trabalho — nada destrói mais a
confiança do usuário do que ver o nome do próprio produto traduzido.

Cache de traduções idênticas: em chamada, saudações e confirmações curtas se repetem
muito.

---
tags: [historico]
atualizado: 2026-08-17
---

# Linha do Tempo

## 2026-08-17 — Concepção e definição de stack

Terceiro projeto, ao lado do Trilha e do NewsTech.

Ideia inicial era Ruby on Rails; descartada com o motivo registrado em
[[Decisões Deliberadas]]. Definida a stack C# / .NET 10 + WPF + NAudio +
Whisper.net, com Rider como IDE.

> [!note] Correção no mesmo dia
> A stack foi definida primeiro sobre .NET 8, no pressuposto de que ele era o LTS
> corrente. Ao conferir a página oficial de download, verificou-se que o 8 sai de
> suporte em 10/11/2026 e que o LTS vigente é o **.NET 10**. Corrigido antes de
> qualquer instalação — nenhum código chegou a ser escrito sobre o 8.

Fechadas as decisões de arquitetura: janela deslizante com LocalAgreement-2, STT
local e nuvem atrás da mesma interface, nuvem como padrão de fábrica.

Decisão explícita de **não dimensionar o produto pela máquina de desenvolvimento**.
Ela mede o piso; o alvo é hardware de cliente.

Criadas 10 tasks de execução e este cofre. Nome do projeto definido: **TLT**.

> [!note] Ainda sem código
> Nada foi construído. A próxima etapa é a fase de descoberta: instalar o SDK, provar
> a captura de loopback e medir o RTF do Whisper para preencher
> [[Requisitos de Hardware]].

## 2026-08-18 — Spikes e fundação

Três spikes rodados, com resultado registrado em vez de suposição:

- **Captura loopback**: funciona. 48 kHz estéreo, buffers fixos de 10 ms. Derrubou a
  armadilha de que o loopback para no silêncio, e descobriu que a flag `Silent` do
  WASAPI não serve para detectar silêncio.
- **RTF do Whisper**: `small` a 13,7x numa GTX 1050 Ti via **Vulkan**, acima do alvo
  de 12. A janela deslizante é viável sem hardware caro. CUDA foi descartado por
  exigir Toolkit no cliente.
- **Tradução local**: LLM generalista de 3B descartado, por latência (2,2 s por frase)
  e por erros que invertem o sentido. Ver [[Tradução Local]].

ADR [[Privacidade por Padrão]] escrito, invertendo o modo padrão para local.

Solution criada e compilando, com 6 testes passando — incluindo um teste de
arquitetura que protege a independência do núcleo.

Ainda pendente para a promessa de privacidade fechar: um caminho de tradução local.

## 2026-08-18 (noite) — O app legenda de verdade

Primeira execução real ponta a ponta: o TLT capturou o áudio de saída do PC,
detectou a fala, transcreveu e mostrou a legenda no overlay, com o texto confirmado
em branco e a hipótese em cinza.

O caminho completo está de pé — captura WASAPI, normalização para 16 kHz mono, VAD
Silero, janela deslizante com LocalAgreement-2, Whisper via Vulkan e overlay.
Latência medida em ritmo real: **927 ms de mediana**, abaixo do alvo de 1,5–3 s.

> [!warning] Ainda não traduz
> O que existe é legenda **no idioma original**. A tradução é a peça que falta, e
> depende de [[Tradução Local]] — sem um caminho local viável, ligar tradução em
> nuvem quebraria a promessa de [[Privacidade por Padrão]].

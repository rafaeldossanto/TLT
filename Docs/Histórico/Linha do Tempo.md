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

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

## É C# / .NET 10 + WPF

- `NAudio.WasapiLoopbackCapture` resolve nativamente a parte mais difícil
- Sintaxe próxima de Java: a curva é de biblioteca, não de linguagem
- Overlay always-on-top é trivial em WPF
- Distribuição limpa: exe self-contained ou MSIX
- .NET 10 por ser o LTS vigente, com suporte até 14/11/2028 — prazo longo importa
  em produto que será vendido e mantido por anos

> [!warning] Não usar .NET 8, mesmo ele ainda aparecendo como LTS
> Verificado na página oficial em 17/08/2026: o .NET 8 está em **manutenção** e sai
> de suporte em **10/11/2026** — menos de três meses. Começar um produto novo nele
> seria dívida no dia um. O .NET 9 também não serve: é STS e termina na mesma data.

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

## API nova do NAudio 3, nunca a obsoleta

`WasapiLoopbackCapture` e `WasapiCapture` estão marcados como **obsoletos** no
NAudio 3. A documentação oficial do projeto ainda ensina os dois, o que engana — a
API vigente é `WasapiRecorderBuilder` / `WasapiRecorder`, descoberta por inspeção do
assembly em 18/08/2026.

Não é renomeação: a API nova habilita coisas que a antiga não tinha, e duas mudam o
produto.

> [!tip] `WithProcessLoopback(pid, modo)` — captura por processo
> Permite capturar o áudio de **um processo específico**, ou de todos menos ele.
> Para o TLT isso significa legendar apenas o app da chamada, ignorando música,
> notificação do sistema e qualquer outro som. Exige Windows 10 build 19041+, que já
> é o TFM adotado. Ver [[Pipeline de Áudio]].

O resto do ganho: `CaptureAsync` devolve `IAsyncEnumerable<AudioBuffer>` em vez de
evento, o buffer carrega flags do WASAPI (`Silent`, `DataDiscontinuity`,
`TimestampError`) e o recorder expõe latência real e controle de cancelamento de eco.

Princípio geral: projeto que nasce hoje e será mantido por anos não começa sobre API
obsoleta, mesmo que ela funcione.

## Aceleração por Vulkan, não CUDA

Medido em 18/08/2026. O Whisper.net oferece `Cpu`, `Cuda`, `Cuda12`, `Vulkan`,
`CoreML`, `OpenVino` e `CpuNoAvx` — e a escolha entre eles decide mais do que
velocidade.

**CUDA foi descartado por um motivo de produto, não de performance.** O pacote
`Whisper.net.Runtime.Cuda` traz `ggml-cuda-whisper.dll` (148 MB) mas **não** traz
`cudart` nem `cublas`, que vêm do CUDA Toolkit. Sem o Toolkit instalado, a carga
falha e o Whisper.net cai silenciosamente para CPU — foi exatamente o que aconteceu
aqui. Exigir que o cliente instale o CUDA Toolkit é inaceitável num produto de
usuário final, e embutir as DLLs engorda o instalador.

**Vulkan resolve os três problemas de uma vez:**

- Roda com o **driver comum** da GPU, sem toolkit nenhum
- É **cross-vendor**: NVIDIA, AMD e Intel com o mesmo binário — um runtime para toda
  a base de clientes, em vez de um caminho por fabricante
- É **menor**: 116 MB contra 312 MB do CUDA (58 MB considerando só win-x64)

E entrega: `small` a RTF 13,7x numa GTX 1050 Ti. Ver [[Requisitos de Hardware]].

> [!important] A seleção não é automática
> O Whisper.net só tenta CUDA e Vulkan se `RuntimeOptions.RuntimeLibraryOrder` for
> configurado explicitamente. No padrão ele usa CPU — e o fallback é **silencioso**,
> sem log nem exceção. Conferir `RuntimeOptions.LoadedLibrary` depois de carregar é
> a única forma de saber o que está rodando de fato. Ver [[Armadilhas Conhecidas]].

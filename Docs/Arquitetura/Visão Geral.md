---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Visão Geral

Solution .NET 10 dividida por responsabilidade, com uma regra central que sustenta
todo o resto:

> [!important] A regra que faz o desenho valer a pena
> `Tlt.Core` não referencia NAudio, nem Whisper.net, nem WPF. Ele contém apenas
> abstrações e modelos. Toda tecnologia concreta vive nas pontas.

Isso não é purismo. São duas apostas concretas: trocar o motor de STT (o campo se
move rápido) e um dia portar a UI para macOS via Avalonia. Se o núcleo souber o que
é NAudio, as duas ficam caras.

## Projetos

| Projeto | Responsabilidade |
|---|---|
| `Tlt.Core` | Abstrações e modelos. Zero dependência externa. |
| `Tlt.Audio` | Captura WASAPI e pré-processamento (NAudio) |
| `Tlt.Stt.Local` | Transcrição local (Whisper.net) |
| `Tlt.Stt.Cloud` | Transcrição em nuvem |
| `Tlt.Translation` | Tradução (API dedicada e LLM) |
| `Tlt.App` | WPF: overlay, preferências, composição da DI |

## Contratos em `Tlt.Core`

- `IAudioSource` — entrega áudio mono 16 kHz pronto para o STT
- `IVoiceActivityDetector` — diz onde há fala
- `ISpeechRecognizer` — áudio para texto, no idioma original
- `ITranslator` — texto para texto, entre idiomas

Modelos: `AudioChunk`, `TranscriptSegment` (com flag **provisório/confirmado** —
central para a janela deslizante, ver [[Pipeline de Áudio]]) e `TranslatedSegment`.

## Fluxo

```
loopback 48kHz stereo
  -> mono + resample 16kHz     (Tlt.Audio)
  -> Channel<AudioChunk>       desacopla a thread de áudio
  -> VAD + segmentação         (Tlt.Core / Tlt.Audio)
  -> ISpeechRecognizer         (Local ou Cloud)
  -> ITranslator               só segmentos confirmados
  -> overlay                   (Tlt.App)
```

## Injeção de dependência

`Microsoft.Extensions.DependencyInjection`, composto em `Tlt.App`. A escolha entre
provider local e nuvem é resolvida em runtime — o usuário troca nas preferências
sem reiniciar.

> [!tip] Equivalências vindas do Spring
> `IServiceCollection` é o container. `AddSingleton/AddScoped/AddTransient` são o
> escopo dos beans. `IOptions<T>` é o `@ConfigurationProperties`. `appsettings.json`
> é o `application.yml`.

## Como está montado (18/08/2026)

A solution existe e compila. Formato **`.slnx`** — o XML novo que o .NET 10 gera por
padrão, no lugar do `.sln` clássico.

```
TLT/
├── Directory.Build.props      propriedades comuns a todos os projetos
├── Directory.Packages.props   versões centralizadas (Central Package Management)
├── global.json                fixa o SDK 10
├── Tlt.slnx
├── src/     Tlt.Core, Tlt.Audio, Tlt.Stt.Local, Tlt.Stt.Cloud, Tlt.Translation, Tlt.App
├── tests/   Tlt.Core.Tests
└── spikes/  código descartável de medição, fora da solution
```

### Target frameworks: só o necessário é Windows

| Projeto | TFM | Por quê |
|---|---|---|
| `Tlt.Core` | `net10.0` | abstrações puras, portável |
| `Tlt.Stt.Local` | `net10.0` | Whisper.net roda em qualquer plataforma |
| `Tlt.Stt.Cloud` | `net10.0` | HTTP |
| `Tlt.Translation` | `net10.0` | idem |
| `Tlt.Audio` | `net10.0-windows10.0.19041.0` | WASAPI; a build 19041 é o mínimo de `WithProcessLoopback` |
| `Tlt.App` | `net10.0-windows10.0.19041.0` | WPF; 19041 também é o mínimo de `WDA_EXCLUDEFROMCAPTURE` |

Só **dois** projetos amarram em Windows. Se um dia entrar Avalonia para macOS, o que
precisa ser reescrito está isolado nesses dois.

### A regra do núcleo virou teste

`Tlt.Core` não tem nenhum `PackageReference`. E isso não depende de disciplina: o
teste `Core_nao_referencia_tecnologia_concreta` inspeciona os assemblies referenciados
e falha o build se NAudio, Whisper, LLama ou WPF aparecerem.

> [!tip] Decisão de arquitetura que se defende sozinha
> Um comentário dizendo "não adicione dependência aqui" é ignorado em seis meses. Um
> teste vermelho, não.

### Warnings são erros

`TreatWarningsAsErrors` está ligado no `Directory.Build.props`. Em projeto novo o
custo de manter zero warnings é baixo; o de recuperar depois de acumular centenas é
alto. A solution compila hoje com **0 avisos**.

### Versões num lugar só

`Directory.Packages.props` com Central Package Management: os `.csproj` citam o pacote
sem versão. É o equivalente ao `dependencyManagement` do Maven ou ao version catalog
do Gradle, e evita o mesmo pacote em versões diferentes entre projetos.

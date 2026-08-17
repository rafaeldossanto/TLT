---
tags: [arquitetura]
atualizado: 2026-08-17
---

# Visão Geral

Solution .NET 8 dividida por responsabilidade, com uma regra central que sustenta
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

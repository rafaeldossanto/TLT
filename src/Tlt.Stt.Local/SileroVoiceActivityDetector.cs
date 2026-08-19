using Tlt.Core.Audio;
using Whisper.net;
using Whisper.net.Ggml;

namespace Tlt.Stt.Local;

/// <summary>
/// Detecta fala com o Silero VAD, que o Whisper.net já embarca.
/// </summary>
/// <remarks>
/// Mora neste projeto, e não em Tlt.Audio, porque depende do Whisper.net e do mesmo
/// downloader de modelos. Fazer Tlt.Audio depender de uma biblioteca de STT só para
/// ter VAD inverteria a direção das dependências.
///
/// Preterido ao VAD por energia porque energia simples confunde música de fundo,
/// notificação do sistema e ruído de teclado com fala — e cada falso positivo vira
/// uma transcrição inútil, que custa GPU no modo local e dinheiro no modo nuvem.
/// </remarks>
public sealed class SileroVoiceActivityDetector : IVoiceActivityDetector
{
    private readonly WhisperVadFactory factory;
    private readonly WhisperVadProcessor processor;

    private SileroVoiceActivityDetector(WhisperVadFactory factory, WhisperVadProcessor processor)
    {
        this.factory = factory;
        this.processor = processor;
    }

    public AudioFormat ExpectedFormat => AudioFormat.Whisper;

    /// <summary>Abre o detector, baixando o modelo na primeira vez.</summary>
    public static async Task<SileroVoiceActivityDetector> CreateAsync(
        SileroVadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SileroVadOptions();
        var caminho = options.ModelPath ?? await BaixarModeloAsync(options, cancellationToken).ConfigureAwait(false);

        var factory = WhisperVadFactory.FromPath(caminho);

        var processor = factory.CreateBuilder()
            .WithThreshold(options.Threshold)
            .WithMinSpeechDuration(options.MinSpeechDuration)
            .WithMinSilenceDuration(options.MinSilenceDuration)
            .WithSpeechPadding(options.SpeechPadding)
            // CPU de propósito: o Silero é minúsculo e a GPU é recurso disputado com o
            // reconhecedor, que precisa dela para sustentar a janela deslizante.
            .WithUseGpu(false)
            .Build();

        return new SileroVoiceActivityDetector(factory, processor);
    }

    public IReadOnlyList<SpeechInterval> DetectSpeech(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return [];

        // NoReset mantém o estado entre chamadas, que é o que permite analisar um
        // fluxo contínuo em pedaços sem o detector esquecer o que veio antes.
        var detectados = processor.DetectSpeechNoReset(samples);

        var intervalos = new SpeechInterval[detectados.Count];
        for (var i = 0; i < detectados.Count; i++)
            intervalos[i] = new SpeechInterval(detectados[i].Start, detectados[i].End);

        return intervalos;
    }

    public void Reset() => processor.ResetState();

    private static Task<string> BaixarModeloAsync(SileroVadOptions options, CancellationToken cancellationToken) =>
        ModelCache.GetOrDownloadAsync(
            options.CacheDirectory,
            $"silero-vad-{options.ModelVersion}.bin",
            ct => WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync(options.ModelVersion, ct),
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await processor.DisposeAsync().ConfigureAwait(false);
        factory.Dispose();
    }
}

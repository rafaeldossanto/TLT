using System.Runtime.CompilerServices;
using Tlt.Core.Transcription;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace Tlt.Stt.Local;

/// <summary>
/// Transcreve na própria máquina, com Whisper acelerado por Vulkan.
/// </summary>
/// <remarks>
/// É o provedor padrão do produto: no modo local o áudio não sai do computador, que é
/// a principal alavanca comercial do TLT. Ver o ADR de privacidade.
/// </remarks>
public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
{
    private readonly WhisperFactory factory;
    private readonly WhisperProcessor processor;
    private readonly WhisperOptions options;

    private WhisperSpeechRecognizer(WhisperFactory factory, WhisperProcessor processor, WhisperOptions options)
    {
        this.factory = factory;
        this.processor = processor;
        this.options = options;
    }

    /// <inheritdoc />
    public bool RunsLocally => true;

    /// <inheritdoc />
    public string Backend => RuntimeOptions.LoadedLibrary?.ToString() ?? "não carregado";

    /// <summary>Modelo em uso, para diagnóstico.</summary>
    public string ModelName => $"{options.Model}-{options.Quantization}";

    /// <summary>
    /// Carrega o modelo, baixando na primeira vez.
    /// </summary>
    public static async Task<WhisperSpeechRecognizer> CreateAsync(
        WhisperOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WhisperOptions();

        // Precisa acontecer antes de qualquer carga de modelo: é estático e global no
        // Whisper.net, e a escolha de biblioteca é feita uma vez só.
        RuntimeOptions.RuntimeLibraryOrder = [.. options.RuntimeOrder];

        var caminho = options.ModelPath ?? await ModelCache.GetOrDownloadAsync(
            options.CacheDirectory,
            $"ggml-{options.Model}-{options.Quantization}.bin",
            ct => WhisperGgmlDownloader.Default.GetGgmlModelAsync(options.Model, options.Quantization, ct),
            cancellationToken).ConfigureAwait(false);

        var factory = WhisperFactory.FromPath(caminho, new WhisperFactoryOptions { UseGpu = options.UseGpu });

        var processor = factory.CreateBuilder()
            .WithLanguage(options.Language)
            // Nunca WithTranslate: a tarefa embutida do Whisper traduz apenas PARA
            // inglês, e o destino aqui é português. A tradução é do ITranslator.
            .Build();

        return new WhisperSpeechRecognizer(factory, processor, options);
    }

    /// <inheritdoc />
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        // No Vulkan a primeira inferência compila shaders. Sem aquecer na
        // inicialização, esse custo cai sobre a primeira frase da chamada do usuário —
        // exatamente o pior momento.
        var ruido = new float[AudioSamplesForWarmUp];
        var random = new Random(42);
        for (var i = 0; i < ruido.Length; i++) ruido[i] = (float)(random.NextDouble() - 0.5) * 0.01f;

        await foreach (var _ in processor.ProcessAsync(ruido, cancellationToken).ConfigureAwait(false))
        {
            // resultado descartado de propósito
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var segmento in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            yield return new TranscriptSegment(
                segmento.Text.Trim(),
                segmento.Start,
                segmento.End,
                segmento.Language ?? options.Language,
                // Falso de propósito. Confirmar é decisão da política de streaming, que
                // sabe se aquele áudio ainda pode ser revisado; o reconhecedor não tem
                // essa informação. Marcar como confirmado aqui faria texto provisório
                // seguir para a tradução e para a tela como se fosse definitivo.
                IsConfirmed: false);
        }
    }

    private const int AudioSamplesForWarmUp = 16_000;   // 1 s a 16 kHz

    public async ValueTask DisposeAsync()
    {
        await processor.DisposeAsync().ConfigureAwait(false);
        factory.Dispose();
    }
}

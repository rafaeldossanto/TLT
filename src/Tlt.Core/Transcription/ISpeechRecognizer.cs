namespace Tlt.Core.Transcription;

/// <summary>
/// Converte áudio em texto no idioma de origem. Não traduz.
/// </summary>
public interface ISpeechRecognizer : IAsyncDisposable
{
    /// <summary>
    /// Falso quando o áudio sai da máquina. O ADR de privacidade proíbe ativar um
    /// reconhecedor remoto sem escolha explícita do usuário, e a interface precisa
    /// mostrar esse estado enquanto ele durar.
    /// </summary>
    bool RunsLocally { get; }

    /// <summary>
    /// Backend efetivamente em uso, por exemplo "Vulkan" ou "Cpu".
    /// </summary>
    /// <remarks>
    /// Existe porque a queda para CPU é silenciosa: sem exceção, sem log, apenas dez
    /// vezes mais lento. Precisa ser observável em diagnóstico.
    /// </remarks>
    string Backend { get; }

    /// <summary>Prepara o modelo e aquece o backend antes do primeiro uso real.</summary>
    /// <remarks>
    /// No Vulkan a primeira inferência compila shaders. Sem aquecer na inicialização,
    /// esse custo cai sobre a primeira frase da chamada do usuário.
    /// </remarks>
    Task WarmUpAsync(CancellationToken cancellationToken = default);

    /// <summary>Transcreve um buffer de áudio em 16 kHz mono.</summary>
    IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        CancellationToken cancellationToken = default);
}

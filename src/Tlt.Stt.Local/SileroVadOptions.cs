using Whisper.net.Ggml;

namespace Tlt.Stt.Local;

/// <summary>
/// Configuração do detector de fala Silero.
/// </summary>
public sealed class SileroVadOptions
{
    /// <summary>Versão do modelo Silero.</summary>
    public SileroVadType ModelVersion { get; init; } = SileroVadType.V5_1_2;

    /// <summary>Caminho do modelo. Quando ausente, é baixado e guardado em cache.</summary>
    public string? ModelPath { get; init; }

    /// <summary>Diretório de cache dos modelos baixados.</summary>
    public string CacheDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TLT", "models");

    /// <summary>Confiança mínima para considerar que há fala.</summary>
    /// <remarks>
    /// Subir reduz falso positivo com música e ruído, mas passa a perder fala baixa.
    /// Calibrar ouvindo chamadas reais.
    /// </remarks>
    public float Threshold { get; init; } = 0.5f;

    /// <summary>Fala mais curta que isto é ignorada.</summary>
    public TimeSpan MinSpeechDuration { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Silêncio necessário para encerrar um trecho de fala.</summary>
    /// <remarks>
    /// Curto demais quebra a frase nas pausas naturais de quem está pensando; longo
    /// demais atrasa a legenda. 600 ms é o meio-termo inicial.
    /// </remarks>
    public TimeSpan MinSilenceDuration { get; init; } = TimeSpan.FromMilliseconds(600);

    /// <summary>Folga incluída antes e depois da fala detectada.</summary>
    /// <remarks>
    /// Sem padding o detector corta consoantes fracas no início e no fim das palavras,
    /// e a transcrição perde justamente o som que distingue palavras parecidas.
    /// </remarks>
    public TimeSpan SpeechPadding { get; init; } = TimeSpan.FromMilliseconds(150);
}

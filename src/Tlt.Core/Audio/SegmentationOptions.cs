namespace Tlt.Core.Audio;

/// <summary>
/// Parâmetros da segmentação de fala.
/// </summary>
/// <remarks>
/// Configuráveis de propósito: a calibração vai sair de ouvir chamadas reais, e
/// recompilar a cada ajuste é desperdício.
/// </remarks>
public sealed class SegmentationOptions
{
    /// <summary>Com que frequência o detector é consultado.</summary>
    /// <remarks>
    /// Menor valor reage mais rápido e custa mais CPU. 500 ms mantém a percepção de
    /// tempo real sem transformar o VAD em gargalo.
    /// </remarks>
    public TimeSpan AnalysisInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Teto de duração de um trecho antes do corte forçado.</summary>
    /// <remarks>
    /// Existe para quem fala sem pausar: sem o teto, o segmento cresceria
    /// indefinidamente e a legenda nunca apareceria.
    /// </remarks>
    public TimeSpan MaxSegmentDuration { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Trechos mais curtos que isto são descartados.</summary>
    /// <remarks>
    /// Filtra tosse, clique de mouse e sílaba solta, que gerariam chamadas de
    /// transcrição inúteis.
    /// </remarks>
    public TimeSpan MinSegmentDuration { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Folga exigida entre o fim da fala e o fim do áudio analisado para considerar o
    /// trecho encerrado.
    /// </summary>
    /// <remarks>
    /// Sem essa margem, uma frase que ainda está sendo dita seria cortada só porque o
    /// buffer analisado terminou ali — o resultado é meia frase indo para a tradução.
    /// </remarks>
    public TimeSpan EndOfSpeechMargin { get; init; } = TimeSpan.FromMilliseconds(250);
}

namespace Tlt.Core.Audio;

/// <summary>
/// Separa fala de silêncio, música e ruído.
/// </summary>
/// <remarks>
/// Precisa olhar o conteúdo das amostras. O spike de captura mediu que a flag
/// <c>Silent</c> do WASAPI não é levantada nem com silêncio digital absoluto, então
/// ela não serve como atalho.
/// </remarks>
public interface IVoiceActivityDetector
{
    /// <summary>Indica se há fala no trecho.</summary>
    bool ContainsSpeech(ReadOnlySpan<float> samples);
}

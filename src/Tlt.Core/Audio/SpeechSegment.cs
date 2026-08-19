namespace Tlt.Core.Audio;

/// <summary>
/// Um trecho de fala fechado, pronto para transcrição.
/// </summary>
/// <param name="Samples">Áudio do trecho, no formato do pipeline.</param>
/// <param name="Start">Posição do início desde o começo da captura.</param>
/// <param name="Duration">Duração do trecho.</param>
/// <param name="ClosedByTimeout">
/// Verdadeiro quando o corte veio do limite de duração e não de uma pausa na fala.
/// Interessa porque um trecho cortado no meio da frase transcreve pior, e o consumidor
/// pode querer tratá-lo com mais cuidado.
/// </param>
public sealed record SpeechSegment(
    ReadOnlyMemory<float> Samples,
    TimeSpan Start,
    TimeSpan Duration,
    bool ClosedByTimeout);

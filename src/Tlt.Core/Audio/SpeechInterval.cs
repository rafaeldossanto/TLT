namespace Tlt.Core.Audio;

/// <summary>
/// Trecho de áudio onde há fala.
/// </summary>
/// <param name="Start">Início, relativo ao buffer analisado.</param>
/// <param name="End">Fim, relativo ao buffer analisado.</param>
public readonly record struct SpeechInterval(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
}

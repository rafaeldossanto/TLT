namespace Tlt.Core.Transcription;

/// <summary>
/// Um trecho transcrito, provisório ou definitivo.
/// </summary>
/// <param name="Text">Texto no idioma de origem.</param>
/// <param name="Start">Início do trecho no áudio.</param>
/// <param name="End">Fim do trecho no áudio.</param>
/// <param name="Language">Idioma de origem.</param>
/// <param name="IsConfirmed">
/// Falso enquanto a janela deslizante ainda pode revisar o texto; verdadeiro quando
/// duas passagens concordaram ou o VAD detectou pausa. Só o que está confirmado vai
/// para a tradução — traduzir hipótese multiplica custo e faz o texto dançar na tela.
/// </param>
public sealed record TranscriptSegment(
    string Text,
    TimeSpan Start,
    TimeSpan End,
    string Language,
    bool IsConfirmed)
{
    /// <summary>Duração do trecho.</summary>
    public TimeSpan Duration => End - Start;
}

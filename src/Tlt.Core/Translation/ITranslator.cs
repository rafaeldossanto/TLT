using Tlt.Core.Transcription;

namespace Tlt.Core.Translation;

/// <summary>
/// Traduz trechos já confirmados pela transcrição.
/// </summary>
public interface ITranslator : IAsyncDisposable
{
    /// <summary>
    /// Falso quando o texto sai da máquina. Note que o texto transcrito é o conteúdo
    /// da conversa: um tradutor remoto anula a privacidade mesmo com a transcrição
    /// rodando local. Ver o ADR de privacidade.
    /// </summary>
    bool RunsLocally { get; }

    /// <summary>Traduz um trecho, usando os anteriores como contexto.</summary>
    /// <param name="segment">Trecho a traduzir. Deve estar confirmado.</param>
    /// <param name="context">
    /// Traduções recentes, em ordem. É o que mantém terminologia consistente e resolve
    /// pronomes em reunião técnica.
    /// </param>
    Task<TranslatedSegment> TranslateAsync(
        TranscriptSegment segment,
        IReadOnlyList<string> context,
        CancellationToken cancellationToken = default);
}

using Tlt.Core.Transcription;

namespace Tlt.Core.Translation;

/// <summary>
/// Um trecho traduzido, junto do original que o gerou.
/// </summary>
/// <param name="Source">Trecho transcrito de origem.</param>
/// <param name="Text">Tradução.</param>
/// <param name="TargetLanguage">Idioma de destino.</param>
public sealed record TranslatedSegment(
    TranscriptSegment Source,
    string Text,
    string TargetLanguage);

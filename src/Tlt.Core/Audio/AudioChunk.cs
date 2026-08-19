namespace Tlt.Core.Audio;

/// <summary>
/// Um bloco de áudio capturado, já em amostras normalizadas.
/// </summary>
/// <param name="Samples">Amostras em ponto flutuante, no formato declarado pela fonte.</param>
/// <param name="Timestamp">Posição do bloco desde o início da captura.</param>
/// <param name="HasDiscontinuity">
/// Verdadeiro quando houve áudio perdido antes deste bloco. Medido no spike de captura:
/// o WASAPI sinaliza isso explicitamente, e é a única forma confiável de detectar perda.
/// Sem observar esta flag, o sintoma aparece bem mais tarde, como transcrição estranha
/// sem erro nenhum no log.
/// </param>
public readonly record struct AudioChunk(
    ReadOnlyMemory<float> Samples,
    TimeSpan Timestamp,
    bool HasDiscontinuity);

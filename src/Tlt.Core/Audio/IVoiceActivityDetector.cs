namespace Tlt.Core.Audio;

/// <summary>
/// Localiza os trechos com fala dentro de um buffer de áudio.
/// </summary>
/// <remarks>
/// Devolve intervalos, e não apenas um sim ou não, porque o segmentador precisa saber
/// **onde** a fala começa e termina para cortar frases — um booleano diria apenas que
/// existe fala em algum lugar do buffer.
///
/// A detecção olha o conteúdo das amostras. A flag <c>Silent</c> do WASAPI não serve
/// de atalho: foi medida em zero mesmo com silêncio digital absoluto.
/// </remarks>
public interface IVoiceActivityDetector : IAsyncDisposable
{
    /// <summary>Formato de áudio que o detector espera.</summary>
    AudioFormat ExpectedFormat { get; }

    /// <summary>
    /// Encontra os trechos com fala. As posições são relativas ao início do buffer.
    /// </summary>
    IReadOnlyList<SpeechInterval> DetectSpeech(ReadOnlySpan<float> samples);

    /// <summary>Descarta o estado interno acumulado entre chamadas.</summary>
    void Reset();
}

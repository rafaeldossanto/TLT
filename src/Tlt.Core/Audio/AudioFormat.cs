namespace Tlt.Core.Audio;

/// <summary>
/// Formato do áudio que trafega no pipeline.
/// </summary>
/// <param name="SampleRate">Amostras por segundo.</param>
/// <param name="Channels">Número de canais.</param>
public readonly record struct AudioFormat(int SampleRate, int Channels)
{
    /// <summary>
    /// O que o Whisper consome: 16 kHz mono. Todo o pipeline converge para cá.
    /// </summary>
    public static readonly AudioFormat Whisper = new(16_000, 1);

    public int BytesPerSecond => SampleRate * Channels * sizeof(float);
}

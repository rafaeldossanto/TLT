using Tlt.Core.Audio;

namespace Tlt.Core.Tests;

public class AudioFormatTests
{
    [Fact]
    public void Formato_do_Whisper_e_16kHz_mono()
    {
        Assert.Equal(16_000, AudioFormat.Whisper.SampleRate);
        Assert.Equal(1, AudioFormat.Whisper.Channels);
    }

    [Theory]
    [InlineData(16_000, 1, 64_000)]    // destino do pipeline
    [InlineData(48_000, 2, 384_000)]   // o que o Windows entrega na captura loopback
    public void Calcula_bytes_por_segundo(int taxa, int canais, int esperado)
    {
        Assert.Equal(esperado, new AudioFormat(taxa, canais).BytesPerSecond);
    }
}

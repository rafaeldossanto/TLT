using Tlt.Audio;
using Tlt.Core.Audio;

namespace Tlt.Audio.Tests;

public class AudioNormalizerTests
{
    [Fact]
    public void Downmix_estereo_tira_a_media_dos_canais()
    {
        // dois quadros: (1.0, 0.0) e (0.5, -0.5)
        float[] intercalado = [1.0f, 0.0f, 0.5f, -0.5f];
        var mono = new float[2];

        AudioNormalizer.Downmix(intercalado, canais: 2, mono);

        Assert.Equal(0.5f, mono[0], precision: 5);
        Assert.Equal(0.0f, mono[1], precision: 5);
    }

    [Fact]
    public void Downmix_usa_media_e_nao_soma()
    {
        // Somar dois canais no maximo estouraria o intervalo [-1, 1] e saturaria,
        // o que apareceria como distorcao justamente nos trechos altos.
        float[] intercalado = [1.0f, 1.0f];
        var mono = new float[1];

        AudioNormalizer.Downmix(intercalado, canais: 2, mono);

        Assert.Equal(1.0f, mono[0], precision: 5);
    }

    [Fact]
    public void Downmix_mono_apenas_copia()
    {
        float[] entrada = [0.1f, -0.2f, 0.3f];
        var mono = new float[3];

        AudioNormalizer.Downmix(entrada, canais: 1, mono);

        Assert.Equal(entrada, mono);
    }

    [Fact]
    public void Reamostragem_de_48k_para_16k_reduz_a_contagem_a_um_terco()
    {
        var normalizer = new AudioNormalizer(new AudioFormat(48_000, 1), AudioFormat.Whisper);

        var total = ProcessarSegundo(normalizer, Seno(1_000, 48_000, 48_000)).Length;

        // Um segundo entra a 48 kHz, um segundo sai a 16 kHz. A tolerancia cobre o
        // atraso do filtro, que segura algumas amostras no comeco.
        Assert.InRange(total, 15_500, 16_100);
    }

    [Fact]
    public void Reamostragem_filtra_frequencia_acima_de_Nyquist()
    {
        // 16 kHz de saida tem Nyquist em 8 kHz, entao um tom de 12 kHz nao cabe.
        // Sem filtro passa-baixa, decimar refletiria esse tom de volta para dentro da
        // banda (aliasing) e ele apareceria como 4 kHz com energia cheia — ruido que
        // o reconhecedor recebe como se fosse sinal.
        var normalizer = new AudioNormalizer(new AudioFormat(48_000, 1), AudioFormat.Whisper);

        var saida = ProcessarSegundo(normalizer, Seno(12_000, 48_000, 48_000));

        Assert.True(Rms(saida) < 0.1, $"o tom de 12 kHz deveria ter sido atenuado, mas o RMS ficou em {Rms(saida):F3}");
    }

    [Fact]
    public void Reamostragem_preserva_frequencia_dentro_da_banda()
    {
        // Controle do teste anterior: se tudo fosse atenuado, aquele passaria por
        // motivo errado. Um tom de 1 kHz esta bem dentro da banda e deve sobreviver.
        var normalizer = new AudioNormalizer(new AudioFormat(48_000, 1), AudioFormat.Whisper);

        var saida = ProcessarSegundo(normalizer, Seno(1_000, 48_000, 48_000));

        Assert.True(Rms(saida) > 0.5, $"o tom de 1 kHz deveria ter passado, mas o RMS ficou em {Rms(saida):F3}");
    }

    [Fact]
    public void Sem_mudanca_de_taxa_o_audio_passa_intacto()
    {
        var normalizer = new AudioNormalizer(AudioFormat.Whisper, AudioFormat.Whisper);
        float[] entrada = [0.1f, -0.2f, 0.3f, 0.4f];
        var saida = new float[8];

        var escritas = normalizer.Process(entrada, saida);

        Assert.Equal(4, escritas);
        Assert.Equal(entrada, saida[..4]);
    }

    [Fact]
    public void Destino_estereo_e_rejeitado()
    {
        // O reconhecedor consome mono. Aceitar estereo aqui adiaria o erro para bem
        // longe do ponto onde ele foi cometido.
        Assert.Throws<ArgumentException>(() =>
            new AudioNormalizer(new AudioFormat(48_000, 2), new AudioFormat(16_000, 2)));
    }

    /// <summary>Processa em blocos de 10 ms, como o WASAPI entrega na prática.</summary>
    private static float[] ProcessarSegundo(AudioNormalizer normalizer, float[] entrada)
    {
        const int bloco = 480;   // 10 ms a 48 kHz
        var acumulado = new List<float>();
        var saida = new float[normalizer.MaxOutputSamples(bloco)];

        for (var i = 0; i + bloco <= entrada.Length; i += bloco)
        {
            var escritas = normalizer.Process(entrada.AsSpan(i, bloco), saida);
            acumulado.AddRange(saida.AsSpan(0, escritas));
        }

        return [.. acumulado];
    }

    private static float[] Seno(double frequencia, int taxa, int amostras)
    {
        var buffer = new float[amostras];
        for (var i = 0; i < amostras; i++)
            buffer[i] = (float)Math.Sin(2 * Math.PI * frequencia * i / taxa);
        return buffer;
    }

    /// <summary>Energia do sinal, descartando o transiente inicial do filtro.</summary>
    private static double Rms(float[] amostras)
    {
        const int descarte = 2_000;
        if (amostras.Length <= descarte) return 0;

        double soma = 0;
        for (var i = descarte; i < amostras.Length; i++) soma += amostras[i] * (double)amostras[i];
        return Math.Sqrt(soma / (amostras.Length - descarte));
    }
}

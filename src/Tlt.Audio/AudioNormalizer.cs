using NAudio.Dsp;
using Tlt.Core.Audio;

namespace Tlt.Audio;

/// <summary>
/// Converte o áudio do formato entregue pelo dispositivo para o formato que o
/// reconhecedor consome: mistura os canais em mono e reamostra.
/// </summary>
/// <remarks>
/// Tem estado — o reamostrador mantém histórico entre blocos, que é justamente o que
/// evita descontinuidade nas emendas. Uma instância por sessão de captura, nunca
/// compartilhada entre threads.
/// </remarks>
public sealed class AudioNormalizer
{
    private readonly AudioFormat origem;
    private readonly AudioFormat destino;
    private readonly WdlResampler? resampler;
    private float[] monoBuffer = [];

    public AudioNormalizer(AudioFormat origem, AudioFormat destino)
    {
        if (origem.Channels < 1) throw new ArgumentException("A origem precisa de ao menos um canal.", nameof(origem));
        if (destino.Channels != 1) throw new ArgumentException("O destino deve ser mono.", nameof(destino));

        this.origem = origem;
        this.destino = destino;

        if (origem.SampleRate == destino.SampleRate) return;

        // Reamostrar por decimação simples (pegar uma amostra a cada três, de 48k para
        // 16k) dobraria as frequências acima de 8 kHz de volta para dentro da banda —
        // aliasing. O WdlResampler aplica o filtro passa-baixa antes, e o custo de
        // errar isso é uma degradação silenciosa na transcrição.
        resampler = new WdlResampler();
        resampler.SetMode(interp: true, filtercnt: 2, sinc: false, sinc_size: 0, sinc_interpsize: 0);
        resampler.SetFilterParms();
        resampler.SetFeedMode(wantInputDriven: true);
        resampler.SetRates(origem.SampleRate, destino.SampleRate);
    }

    /// <summary>Quantas amostras de saída cabem, no pior caso, para uma entrada deste tamanho.</summary>
    public int MaxOutputSamples(int inputSampleCount)
    {
        var quadros = inputSampleCount / origem.Channels;
        var proporcao = (double)destino.SampleRate / origem.SampleRate;
        return (int)(quadros * proporcao) + 64;   // folga para o estado interno do filtro
    }

    /// <summary>
    /// Normaliza um bloco. Devolve quantas amostras foram escritas em <paramref name="saida"/>,
    /// que pode ser zero enquanto o reamostrador acumula histórico.
    /// </summary>
    public int Process(ReadOnlySpan<float> entrada, Span<float> saida)
    {
        var quadros = entrada.Length / origem.Channels;
        if (quadros == 0) return 0;

        if (monoBuffer.Length < quadros) monoBuffer = new float[quadros];
        var mono = monoBuffer.AsSpan(0, quadros);
        Downmix(entrada, origem.Channels, mono);

        if (resampler is null)
        {
            var copiar = Math.Min(mono.Length, saida.Length);
            mono[..copiar].CopyTo(saida);
            return copiar;
        }

        var aceitas = resampler.ResamplePrepare(quadros, destino.Channels, out var entradaResampler);
        var usadas = Math.Min(aceitas, quadros);
        mono[..usadas].CopyTo(entradaResampler);

        return resampler.ResampleOut(saida, usadas, saida.Length, destino.Channels);
    }

    /// <summary>
    /// Mistura canais intercalados em mono, pela média.
    /// </summary>
    /// <remarks>
    /// Média e não soma: somar estoura o intervalo [-1, 1] e satura, o que aparece
    /// como distorção justamente nos trechos altos.
    /// </remarks>
    public static void Downmix(ReadOnlySpan<float> intercalado, int canais, Span<float> mono)
    {
        if (canais == 1)
        {
            intercalado[..mono.Length].CopyTo(mono);
            return;
        }

        for (var quadro = 0; quadro < mono.Length; quadro++)
        {
            var soma = 0f;
            var inicio = quadro * canais;
            for (var canal = 0; canal < canais; canal++) soma += intercalado[inicio + canal];
            mono[quadro] = soma / canais;
        }
    }
}

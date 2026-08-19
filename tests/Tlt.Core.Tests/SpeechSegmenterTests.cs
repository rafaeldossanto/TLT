using Tlt.Core.Audio;

namespace Tlt.Core.Tests;

public class SpeechSegmenterTests
{
    private static readonly AudioFormat Formato = AudioFormat.Whisper;

    [Fact]
    public async Task Emite_o_trecho_quando_a_fala_termina_com_silencio_depois()
    {
        // O detector aponta fala no primeiro segundo; o buffer chega a dois segundos,
        // entao sobrou silencio suficiente para dar a frase por encerrada.
        var vad = new VadFalso(_ => [new SpeechInterval(TimeSpan.Zero, TimeSpan.FromSeconds(1))]);
        var segmenter = new SpeechSegmenter(vad);

        var segmentos = await Coletar(segmenter, Audio(segundos: 2));

        var segmento = Assert.Single(segmentos);
        Assert.Equal(1.0, segmento.Duration.TotalSeconds, precision: 1);
        Assert.False(segmento.ClosedByTimeout);
    }

    [Fact]
    public async Task Nao_emite_enquanto_a_fala_alcanca_o_fim_do_buffer()
    {
        // Intervalo colado no fim do audio analisado significa frase ainda em curso.
        // Cortar aqui mandaria meia frase para a traducao.
        var vad = new VadFalso(amostras => [new SpeechInterval(TimeSpan.Zero, Duracao(amostras))]);
        var segmenter = new SpeechSegmenter(vad);

        var segmentos = await Coletar(segmenter, Audio(segundos: 3));

        Assert.Empty(segmentos);
    }

    [Fact]
    public async Task Corta_a_forca_quem_fala_sem_pausar()
    {
        var vad = new VadFalso(amostras => [new SpeechInterval(TimeSpan.Zero, Duracao(amostras))]);
        var opcoes = new SegmentationOptions { MaxSegmentDuration = TimeSpan.FromSeconds(5) };
        var segmenter = new SpeechSegmenter(vad, opcoes);

        var segmentos = await Coletar(segmenter, Audio(segundos: 12));

        Assert.NotEmpty(segmentos);
        Assert.All(segmentos, s => Assert.True(s.ClosedByTimeout));
        Assert.All(segmentos, s => Assert.True(s.Duration <= TimeSpan.FromSeconds(5.1)));
    }

    [Fact]
    public async Task Descarta_trecho_curto_demais()
    {
        // 100 ms de "fala" e tosse ou clique, nao frase. Transcrever isso seria
        // gastar GPU ou dinheiro de API a toa.
        var vad = new VadFalso(_ => [new SpeechInterval(TimeSpan.Zero, TimeSpan.FromMilliseconds(100))]);
        var segmenter = new SpeechSegmenter(vad);

        var segmentos = await Coletar(segmenter, Audio(segundos: 2));

        Assert.Empty(segmentos);
    }

    [Fact]
    public async Task Descontinuidade_descarta_o_que_estava_acumulado()
    {
        // Emendar o audio de antes com o de depois de uma perda produz uma frase
        // costurada por cima de um buraco, e a transcricao sai sem sentido aparente.
        var vad = new VadFalso(_ => [new SpeechInterval(TimeSpan.Zero, TimeSpan.FromSeconds(1))]);
        var segmenter = new SpeechSegmenter(vad);

        var segmentos = await Coletar(segmenter, AudioComPerdaNoMeio());

        Assert.Equal(1, vad.Resets);
    }

    [Fact]
    public async Task Posicao_do_trecho_acumula_ao_longo_da_captura()
    {
        var chamadas = 0;
        var vad = new VadFalso(_ =>
        {
            chamadas++;
            return [new SpeechInterval(TimeSpan.Zero, TimeSpan.FromSeconds(1))];
        });
        var segmenter = new SpeechSegmenter(vad);

        var segmentos = await Coletar(segmenter, Audio(segundos: 6));

        Assert.True(segmentos.Count >= 2, "esperava mais de um trecho em seis segundos");
        // O segundo trecho tem que comecar depois do primeiro: a posicao e absoluta
        // desde o inicio da captura, nao relativa ao buffer de trabalho.
        Assert.True(segmentos[1].Start > segmentos[0].Start);
    }

    private static TimeSpan Duracao(int amostras) => TimeSpan.FromSeconds((double)amostras / Formato.SampleRate);

    /// <summary>Áudio em blocos de 100 ms, como o pipeline entrega.</summary>
    private static async IAsyncEnumerable<AudioChunk> Audio(int segundos)
    {
        const int porBloco = 1_600;   // 100 ms a 16 kHz
        var blocos = segundos * 10;
        for (var i = 0; i < blocos; i++)
        {
            yield return new AudioChunk(new float[porBloco], TimeSpan.FromMilliseconds(i * 100), HasDiscontinuity: false);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<AudioChunk> AudioComPerdaNoMeio()
    {
        const int porBloco = 1_600;
        for (var i = 0; i < 20; i++)
        {
            yield return new AudioChunk(new float[porBloco], TimeSpan.FromMilliseconds(i * 100), HasDiscontinuity: i == 10);
            await Task.Yield();
        }
    }

    private static async Task<List<SpeechSegment>> Coletar(SpeechSegmenter segmenter, IAsyncEnumerable<AudioChunk> audio)
    {
        var resultado = new List<SpeechSegment>();
        await foreach (var s in segmenter.SegmentAsync(audio)) resultado.Add(s);
        return resultado;
    }

    private sealed class VadFalso(Func<int, IReadOnlyList<SpeechInterval>> regra) : IVoiceActivityDetector
    {
        public AudioFormat ExpectedFormat => Formato;
        public int Resets { get; private set; }
        public IReadOnlyList<SpeechInterval> DetectSpeech(ReadOnlySpan<float> samples) => regra(samples.Length);
        public void Reset() => Resets++;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

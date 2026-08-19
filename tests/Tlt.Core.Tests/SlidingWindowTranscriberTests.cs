using System.Runtime.CompilerServices;
using Tlt.Core.Audio;
using Tlt.Core.Transcription;

namespace Tlt.Core.Tests;

public class SlidingWindowTranscriberTests
{
    private static readonly SlidingWindowOptions Rapido = new()
    {
        ReprocessInterval = TimeSpan.FromMilliseconds(500),
        WindowDuration = TimeSpan.FromSeconds(10),
    };

    [Fact]
    public async Task Confirma_o_prefixo_em_que_duas_passagens_concordam()
    {
        // O coracao do LocalAgreement-2: "the quick brown" aparece igual nas duas
        // passadas, entao e dado por estavel. "fox" so apareceu na segunda e ainda
        // pode mudar.
        var stt = new ReconhecedorFalso("the quick brown", "the quick brown fox");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        // O que importa e a ORDEM: o prefixo estavel sai confirmado durante a fala.
        // "fox" so vira definitivo no fechamento do fluxo, depois dele.
        var confirmados = segmentos.Where(s => s.IsConfirmed).Select(s => s.Text).ToList();
        Assert.Equal("the quick brown", confirmados[0]);
    }

    [Fact]
    public async Task Nao_confirma_texto_que_a_passada_seguinte_mudou()
    {
        // A segunda passada corrigiu "brown" para "green". Confirmar na primeira teria
        // colocado na tela um texto que o modelo depois desmentiu.
        var stt = new ReconhecedorFalso("the quick brown", "the quick green");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        // "brown" nunca pode virar definitivo: a passada seguinte o desmentiu. Ja
        // "green" e confirmado no fechamento, e isso esta certo — e a ultima palavra
        // que o modelo deu.
        var confirmados = segmentos.Where(s => s.IsConfirmed).ToList();
        Assert.DoesNotContain(confirmados, s => s.Text.Contains("brown"));
    }

    [Fact]
    public async Task Fim_do_fluxo_confirma_o_que_estava_pendente()
    {
        // Quando o audio acaba nao havera outra passada. Deixar o texto provisorio
        // para sempre significaria nunca traduzi-lo nem fixa-lo na tela.
        var stt = new ReconhecedorFalso("alpha", "alpha beta");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        var confirmadas = segmentos.Where(s => s.IsConfirmed).SelectMany(s => s.Text.Split(' ')).ToList();
        Assert.Contains("alpha", confirmadas);
        Assert.Contains("beta", confirmadas);
    }

    [Fact]
    public async Task Emite_hipotese_provisoria_enquanto_a_pessoa_fala()
    {
        // E o que faz a legenda aparecer durante a fala, em vez de surgir em bloco no
        // fim da frase.
        var stt = new ReconhecedorFalso("hello world");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        Assert.Contains(segmentos, s => !s.IsConfirmed);
    }

    [Fact]
    public async Task Fim_de_fala_confirma_tudo_de_uma_vez()
    {
        // Sem outra passada pela frente, nao ha o que revisar: o texto todo vira
        // definitivo mesmo sem duas passagens concordarem.
        var stt = new ReconhecedorFalso("good morning everyone");
        var transcriber = new SlidingWindowTranscriber(FalaEncerrada(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        var confirmado = segmentos.Where(s => s.IsConfirmed).ToList();
        Assert.Contains(confirmado, s => s.Text == "good morning everyone");
        Assert.DoesNotContain(segmentos, s => !s.IsConfirmed);
    }

    [Fact]
    public async Task Nao_reemite_o_que_ja_confirmou()
    {
        // Reemitir faria a legenda repetir palavras a cada passada.
        var stt = new ReconhecedorFalso("one two", "one two three", "one two three four");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, Rapido);

        var segmentos = await Coletar(transcriber, Audio(segundos: 2));

        var confirmadas = segmentos.Where(s => s.IsConfirmed).SelectMany(s => s.Text.Split(' ')).ToList();
        Assert.Equal(confirmadas.Count, confirmadas.Distinct().Count());
    }

    [Fact]
    public async Task Sem_hipoteses_so_sai_texto_confirmado()
    {
        var opcoes = new SlidingWindowOptions
        {
            ReprocessInterval = TimeSpan.FromMilliseconds(500),
            EmitHypotheses = false,
        };
        var stt = new ReconhecedorFalso("alpha beta", "alpha beta gamma");
        var transcriber = new SlidingWindowTranscriber(FalaEmCurso(), stt, opcoes);

        var segmentos = await Coletar(transcriber, Audio(segundos: 1));

        Assert.All(segmentos, s => Assert.True(s.IsConfirmed));
    }

    private static IVoiceActivityDetector FalaEmCurso() =>
        new VadFalso(duracao => [new SpeechInterval(TimeSpan.Zero, duracao)]);

    private static IVoiceActivityDetector FalaEncerrada() =>
        new VadFalso(duracao => [new SpeechInterval(TimeSpan.Zero, duracao - TimeSpan.FromSeconds(1))]);

    private static async IAsyncEnumerable<AudioChunk> Audio(int segundos)
    {
        const int porBloco = 1_600;   // 100 ms a 16 kHz
        for (var i = 0; i < segundos * 10; i++)
        {
            yield return new AudioChunk(new float[porBloco], TimeSpan.FromMilliseconds(i * 100), false);
            await Task.Yield();
        }
    }

    private static async Task<List<TranscriptSegment>> Coletar(
        SlidingWindowTranscriber transcriber,
        IAsyncEnumerable<AudioChunk> audio)
    {
        var resultado = new List<TranscriptSegment>();
        await foreach (var s in transcriber.TranscribeAsync(audio)) resultado.Add(s);
        return resultado;
    }

    private sealed class VadFalso(Func<TimeSpan, IReadOnlyList<SpeechInterval>> regra) : IVoiceActivityDetector
    {
        public AudioFormat ExpectedFormat => AudioFormat.Whisper;

        public IReadOnlyList<SpeechInterval> DetectSpeech(ReadOnlySpan<float> samples) =>
            regra(TimeSpan.FromSeconds((double)samples.Length / ExpectedFormat.SampleRate));

        public void Reset() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ReconhecedorFalso(params string[] respostas) : ISpeechRecognizer
    {
        private int chamada;

        public bool RunsLocally => true;
        public string Backend => "falso";
        public Task WarmUpAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
            ReadOnlyMemory<float> samples,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var texto = respostas[Math.Min(chamada++, respostas.Length - 1)];
            yield return new TranscriptSegment(texto, TimeSpan.Zero, TimeSpan.Zero, "en", false);
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

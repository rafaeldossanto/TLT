using System.Runtime.CompilerServices;

namespace Tlt.Core.Audio;

/// <summary>
/// Transforma o fluxo contínuo de áudio em trechos de fala fechados, prontos para
/// transcrição.
/// </summary>
/// <remarks>
/// Fica no núcleo, sem dependência de tecnologia, porque é a política do produto e
/// não um detalhe de infraestrutura — e porque assim é testável com um detector falso,
/// sem placa de som e sem modelo carregado.
/// </remarks>
public sealed class SpeechSegmenter(IVoiceActivityDetector detector, SegmentationOptions? options = null)
{
    private readonly SegmentationOptions options = options ?? new SegmentationOptions();

    // Buffer de trabalho. Cresce enquanto a fala continua e encolhe pela frente
    // conforme os trechos são emitidos. A 16 kHz, o teto de 15 s são 240 mil floats.
    private readonly List<float> buffer = [];

    // Onde o início do buffer cai na linha do tempo da captura, para que os trechos
    // emitidos carreguem posição absoluta e não relativa ao buffer.
    private TimeSpan bufferStart;
    private TimeSpan ultimaAnalise;

    /// <summary>
    /// Consome o áudio e emite os trechos de fala à medida que se fecham.
    /// </summary>
    public async IAsyncEnumerable<SpeechSegment> SegmentAsync(
        IAsyncEnumerable<AudioChunk> audio,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var taxa = detector.ExpectedFormat.SampleRate;

        await foreach (var chunk in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            // Áudio perdido invalida o que estava acumulado: emendar o que veio antes
            // com o que veio depois produziria uma frase costurada por cima de um
            // buraco, e a transcrição sairia sem sentido aparente.
            if (chunk.HasDiscontinuity) Descartar();

            buffer.AddRange(chunk.Samples.Span);

            var duracaoBuffer = Duracao(buffer.Count, taxa);

            if (duracaoBuffer >= options.MaxSegmentDuration)
            {
                var forcado = Extrair(options.MaxSegmentDuration, taxa, porTimeout: true);
                if (forcado is not null) yield return forcado;
                continue;
            }

            if (duracaoBuffer - ultimaAnalise < options.AnalysisInterval) continue;
            ultimaAnalise = duracaoBuffer;

            foreach (var intervalo in detector.DetectSpeech(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer)))
            {
                // Só encerra o trecho se sobrou silêncio observado depois dele. Sem a
                // margem, cortaríamos uma frase ainda em curso apenas porque o buffer
                // analisado acabou naquele ponto.
                if (intervalo.End + options.EndOfSpeechMargin > duracaoBuffer) break;

                var segmento = Extrair(intervalo.End, taxa, porTimeout: false, inicioFala: intervalo.Start);
                if (segmento is not null) yield return segmento;

                duracaoBuffer = Duracao(buffer.Count, taxa);
                ultimaAnalise = TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Retira do buffer tudo até <paramref name="ate"/> e devolve como trecho.
    /// </summary>
    private SpeechSegment? Extrair(TimeSpan ate, int taxa, bool porTimeout, TimeSpan inicioFala = default)
    {
        var amostrasTotais = Math.Min(Amostras(ate, taxa), buffer.Count);
        if (amostrasTotais <= 0) return null;

        var descarte = Math.Min(Amostras(inicioFala, taxa), amostrasTotais);
        var amostrasFala = amostrasTotais - descarte;
        var duracaoFala = Duracao(amostrasFala, taxa);

        SpeechSegment? segmento = null;

        // Trechos curtos demais são tosse, clique ou sílaba solta: consomem o buffer
        // mas não geram chamada de transcrição.
        if (duracaoFala >= options.MinSegmentDuration)
        {
            var amostras = new float[amostrasFala];
            buffer.CopyTo(descarte, amostras, 0, amostrasFala);

            segmento = new SpeechSegment(
                amostras,
                bufferStart + inicioFala,
                duracaoFala,
                porTimeout);
        }

        buffer.RemoveRange(0, amostrasTotais);
        bufferStart += Duracao(amostrasTotais, taxa);
        ultimaAnalise = TimeSpan.Zero;

        return segmento;
    }

    private void Descartar()
    {
        bufferStart += Duracao(buffer.Count, detector.ExpectedFormat.SampleRate);
        buffer.Clear();
        ultimaAnalise = TimeSpan.Zero;
        detector.Reset();
    }

    private static TimeSpan Duracao(int amostras, int taxa) => TimeSpan.FromSeconds((double)amostras / taxa);

    private static int Amostras(TimeSpan duracao, int taxa) => (int)(duracao.TotalSeconds * taxa);
}

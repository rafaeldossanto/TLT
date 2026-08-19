using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tlt.Core.Audio;

namespace Tlt.Core.Transcription;

/// <summary>
/// Transcreve o áudio enquanto a pessoa ainda fala, reprocessando o trecho em curso e
/// confirmando o texto conforme ele se estabiliza.
/// </summary>
/// <remarks>
/// Implementa LocalAgreement-2: o prefixo em que duas passagens consecutivas concordam
/// é dado por estável e confirmado; o resto segue como hipótese revisável.
///
/// Existe porque o Whisper não é streaming. Esperar a frase terminar para transcrever
/// custa a duração inteira da frase em latência: no teste real, uma frase de 11 s
/// levaria mais de 12 segundos até aparecer na tela.
/// </remarks>
public sealed class SlidingWindowTranscriber(
    IVoiceActivityDetector detector,
    ISpeechRecognizer recognizer,
    SlidingWindowOptions? options = null)
{
    private readonly SlidingWindowOptions options = options ?? new SlidingWindowOptions();
    private readonly List<float> buffer = [];

    private string[] hipoteseAnterior = [];
    private int confirmadasEmitidas;
    private TimeSpan bufferStart;
    private TimeSpan ultimoProcessamento;

    /// <summary>
    /// Consome o áudio e emite trechos, provisórios e confirmados, à medida que a fala
    /// acontece.
    /// </summary>
    public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        IAsyncEnumerable<AudioChunk> audio,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var taxa = detector.ExpectedFormat.SampleRate;

        await foreach (var chunk in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            // Áudio perdido invalida o acumulado: emendar os dois lados de um buraco
            // produz uma frase costurada, e o texto sai errado sem nada indicar o
            // motivo.
            if (chunk.HasDiscontinuity)
            {
                foreach (var final in FecharTrecho()) yield return final;
                Limpar();
                continue;
            }

            buffer.AddRange(chunk.Samples.Span);
            var duracao = Duracao(buffer.Count, taxa);

            if (duracao - ultimoProcessamento < options.ReprocessInterval) continue;
            ultimoProcessamento = duracao;

            // O trecho fecha por pausa na fala ou por estourar a janela. O segundo
            // caso existe para quem fala sem pausar: sem ele o buffer cresceria além
            // da janela e o custo por passada subiria junto.
            var fechar = FalaEncerrou(duracao) || duracao >= options.WindowDuration;

            var texto = await Transcrever(cancellationToken).ConfigureAwait(false);
            foreach (var segmento in Reconciliar(texto, fechar, duracao)) yield return segmento;

            if (fechar) Limpar();
        }

        foreach (var final in FecharTrecho()) yield return final;
    }

    /// <summary>
    /// Confronta a transcrição nova com a anterior e decide o que virou definitivo.
    /// </summary>
    private IEnumerable<TranscriptSegment> Reconciliar(string[] atual, bool falaEncerrou, TimeSpan duracao)
    {
        // Fim de fala encerra a discussão: não haverá outra passada para revisar nada.
        var estaveis = falaEncerrou ? atual.Length : PrefixoComum(atual, hipoteseAnterior);
        hipoteseAnterior = atual;

        if (estaveis > confirmadasEmitidas)
        {
            var novas = atual[confirmadasEmitidas..estaveis];
            confirmadasEmitidas = estaveis;
            yield return Montar(novas, duracao, confirmado: true);
        }

        if (!options.EmitHypotheses || falaEncerrou) yield break;

        // O que ainda pode mudar vai como hipótese, para a interface mostrar em cinza.
        var inicio = Math.Min(confirmadasEmitidas, atual.Length);
        var provisorias = atual[inicio..];
        if (provisorias.Length > 0) yield return Montar(provisorias, duracao, confirmado: false);
    }

    private async Task<string[]> Transcrever(CancellationToken cancellationToken)
    {
        var partes = new List<string>();
        var janela = buffer.ToArray();

        await foreach (var s in recognizer.TranscribeAsync(janela, cancellationToken).ConfigureAwait(false))
            partes.Add(s.Text);

        return string.Join(" ", partes)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Emite como confirmado o que ficou pendente quando o fluxo acaba.</summary>
    private IEnumerable<TranscriptSegment> FecharTrecho()
    {
        if (hipoteseAnterior.Length <= confirmadasEmitidas) yield break;

        var restantes = hipoteseAnterior[confirmadasEmitidas..];
        confirmadasEmitidas = hipoteseAnterior.Length;
        yield return Montar(restantes, Duracao(buffer.Count, detector.ExpectedFormat.SampleRate), confirmado: true);
    }

    private TranscriptSegment Montar(string[] palavras, TimeSpan duracao, bool confirmado) =>
        new(string.Join(" ", palavras),
            bufferStart,
            bufferStart + duracao,
            recognizer.RunsLocally ? "local" : "remoto",
            confirmado);

    private bool FalaEncerrou(TimeSpan duracao)
    {
        var intervalos = detector.DetectSpeech(CollectionsMarshal.AsSpan(buffer));
        if (intervalos.Count == 0) return false;

        // Só encerra se sobrou silêncio depois da última fala. Sem a margem, cortaria
        // uma frase ainda em curso apenas porque o buffer analisado terminou ali.
        return intervalos[^1].End + options.EndOfSpeechMargin <= duracao;
    }

    private void Limpar()
    {
        bufferStart += Duracao(buffer.Count, detector.ExpectedFormat.SampleRate);
        buffer.Clear();
        hipoteseAnterior = [];
        confirmadasEmitidas = 0;
        ultimoProcessamento = TimeSpan.Zero;
        detector.Reset();
    }

    /// <summary>Quantas palavras iniciais as duas passagens têm em comum.</summary>
    private static int PrefixoComum(string[] a, string[] b)
    {
        var limite = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < limite && string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) i++;
        return i;
    }

    private static TimeSpan Duracao(int amostras, int taxa) => TimeSpan.FromSeconds((double)amostras / taxa);
}

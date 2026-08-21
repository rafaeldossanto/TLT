using System.Threading.Channels;
using Tlt.App.Overlay;
using Tlt.Audio;
using Tlt.Core.Transcription;
using Tlt.Stt.Local;
using Tlt.Translation;

namespace Tlt.App;

/// <summary>
/// Liga o pipeline ao overlay: captura, detecta fala, transcreve, traduz e mostra.
/// </summary>
public sealed class TranscriptionService(OverlayWindow overlay)
{
    /// <summary>Quantas palavras ficam na tela.</summary>
    /// <remarks>
    /// Numa reunião de uma hora o texto cresceria sem parar. A legenda serve para
    /// acompanhar o que está sendo dito agora, não para ler histórico.
    /// </remarks>
    private const int PalavrasVisiveis = 34;

    /// <summary>Quantas frases anteriores acompanham a tradução como contexto.</summary>
    private const int FrasesDeContexto = 3;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        overlay.DefinirStatus("carregando modelos...");

        await using var vad = await SileroVoiceActivityDetector.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await using var stt = await WhisperSpeechRecognizer.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await using var tradutor = await OpusMtTranslator.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        overlay.DefinirStatus($"aquecendo {stt.ModelName} em {stt.Backend}...");
        await stt.WarmUpAsync(cancellationToken).ConfigureAwait(false);

        await using var fonte = await WasapiLoopbackSource.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var privacidade = stt.RunsLocally && tradutor.RunsLocally ? "100% local" : "usando nuvem";
        overlay.DefinirStatus(
            $"{fonte.DeviceName}  ·  {stt.ModelName} em {stt.Backend}  ·  {privacidade}  ·  Ctrl+Alt+L esconde");

        // A tradução leva cerca de um segundo. Fazê-la dentro do laço de transcrição
        // travaria o consumo de áudio por todo esse tempo, e o áudio acumulado viraria
        // descontinuidade — perda real de fala. A fila desacopla as duas velocidades.
        var fila = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        var traduzindo = TraduzirAsync(tradutor, fila.Reader, cancellationToken);

        try
        {
            var transcriber = new SlidingWindowTranscriber(vad, stt);

            await foreach (var segmento in transcriber
                .TranscribeAsync(fonte.CaptureAsync(cancellationToken), cancellationToken)
                .ConfigureAwait(false))
            {
                if (segmento.IsConfirmed)
                    await fila.Writer.WriteAsync(segmento.Text, cancellationToken).ConfigureAwait(false);
                else
                    overlay.DefinirProvisorio(segmento.Text);
            }
        }
        finally
        {
            fila.Writer.TryComplete();
            await traduzindo.ConfigureAwait(false);
        }
    }

    private async Task TraduzirAsync(
        Tlt.Core.Translation.ITranslator tradutor,
        ChannelReader<string> fila,
        CancellationToken cancellationToken)
    {
        var original = new List<string>();
        var traduzido = new List<string>();
        var contexto = new List<string>();

        await foreach (var texto in fila.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var segmento = new TranscriptSegment(texto, TimeSpan.Zero, TimeSpan.Zero, "en", IsConfirmed: true);
            var resultado = await tradutor.TranslateAsync(segmento, contexto, cancellationToken).ConfigureAwait(false);

            contexto.Add(resultado.Text);
            if (contexto.Count > FrasesDeContexto) contexto.RemoveAt(0);

            Acumular(original, texto);
            Acumular(traduzido, resultado.Text);

            overlay.DefinirOriginal(string.Join(' ', original));
            overlay.DefinirConfirmado(string.Join(' ', traduzido));
            overlay.DefinirProvisorio(string.Empty);
        }
    }

    private static void Acumular(List<string> palavras, string texto)
    {
        palavras.AddRange(texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (palavras.Count > PalavrasVisiveis) palavras.RemoveRange(0, palavras.Count - PalavrasVisiveis);
    }
}

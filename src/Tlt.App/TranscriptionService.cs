using Tlt.App.Overlay;
using Tlt.Audio;
using Tlt.Core.Transcription;
using Tlt.Stt.Local;

namespace Tlt.App;

/// <summary>
/// Liga o pipeline de áudio ao overlay: captura, detecta fala, transcreve e mostra.
/// </summary>
public sealed class TranscriptionService(OverlayWindow overlay)
{
    /// <summary>Quantas palavras confirmadas ficam na tela.</summary>
    /// <remarks>
    /// O texto confirmado cresce sem parar durante uma reunião de uma hora. A legenda
    /// é para acompanhar o que está sendo dito agora, não para ler o histórico.
    /// </remarks>
    private const int PalavrasVisiveis = 40;

    private readonly List<string> confirmadas = [];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        overlay.DefinirStatus("carregando modelos...");

        await using var vad = await SileroVoiceActivityDetector.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await using var stt = await WhisperSpeechRecognizer.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        overlay.DefinirStatus($"aquecendo {stt.ModelName} em {stt.Backend}...");
        await stt.WarmUpAsync(cancellationToken).ConfigureAwait(false);

        await using var fonte = await WasapiLoopbackSource.CreateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // O backend fica visível porque a queda para CPU é silenciosa: sem isso, o
        // usuário só percebe que algo está errado pela lentidão, sem saber o motivo.
        overlay.DefinirStatus($"{fonte.DeviceName}  ·  {stt.ModelName}  ·  {stt.Backend}  ·  Ctrl+Alt+L esconde");

        var transcriber = new SlidingWindowTranscriber(vad, stt);

        await foreach (var segmento in transcriber
            .TranscribeAsync(fonte.CaptureAsync(cancellationToken), cancellationToken)
            .ConfigureAwait(false))
        {
            if (segmento.IsConfirmed)
            {
                confirmadas.AddRange(segmento.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (confirmadas.Count > PalavrasVisiveis)
                    confirmadas.RemoveRange(0, confirmadas.Count - PalavrasVisiveis);

                overlay.DefinirConfirmado(string.Join(' ', confirmadas));
                overlay.DefinirProvisorio(string.Empty);
            }
            else
            {
                overlay.DefinirProvisorio(segmento.Text);
            }
        }
    }
}

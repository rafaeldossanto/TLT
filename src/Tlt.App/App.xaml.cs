using System.Windows;
using Tlt.App.Overlay;

namespace Tlt.App;

/// <summary>
/// Ponto de entrada. Abre o overlay e põe o pipeline para rodar.
/// </summary>
public partial class App : Application
{
    private readonly CancellationTokenSource cancelamento = new();
    private OverlayWindow? overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        overlay = new OverlayWindow();
        overlay.Show();

        var servico = new TranscriptionService(overlay);

        // Roda solto de propósito: carregar modelos e capturar áudio são operações
        // longas, e travar a thread de interface deixaria a janela congelada.
        _ = Task.Run(async () =>
        {
            try
            {
                await servico.RunAsync(cancelamento.Token);
            }
            catch (OperationCanceledException)
            {
                // encerramento normal
            }
            catch (Exception erro)
            {
                // Falhar em silêncio deixaria o overlay parado sem explicação, que é
                // exatamente o pior comportamento possível no meio de uma reunião.
                overlay.DefinirStatus($"erro: {erro.Message}");
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        cancelamento.Cancel();
        cancelamento.Dispose();
        base.OnExit(e);
    }
}

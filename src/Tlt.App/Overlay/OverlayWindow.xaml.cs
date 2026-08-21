using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Tlt.App.Overlay;

/// <summary>
/// Janela de legenda que fica sobre a chamada.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int IdAtalhoVisibilidade = 9001;
    private const uint TeclaL = 0x4C;
    private const int MensagemHotKey = 0x0312;

    private readonly OverlaySettings settings;
    private bool visivel = true;

    public OverlayWindow(OverlaySettings? settings = null)
    {
        InitializeComponent();

        this.settings = settings ?? OverlaySettings.Carregar();
        Width = this.settings.Width;
        RestaurarPosicao();
    }

    /// <summary>Substitui o texto no idioma original.</summary>
    public void DefinirOriginal(string texto) =>
        Dispatcher.Invoke(() => Original.Text = settings.ShowOriginal ? texto : string.Empty);

    /// <summary>Substitui a tradução já confirmada.</summary>
    public void DefinirConfirmado(string texto) => Dispatcher.Invoke(() => Confirmado.Text = texto);

    /// <summary>Substitui a hipótese em revisão.</summary>
    public void DefinirProvisorio(string texto) =>
        Dispatcher.Invoke(() => Provisorio.Text = settings.ShowHypotheses ? texto : string.Empty);

    /// <summary>Atualiza a linha de diagnóstico.</summary>
    public void DefinirStatus(string texto) => Dispatcher.Invoke(() => Status.Text = texto);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var origem = (HwndSource)PresentationSource.FromVisual(this)!;
        origem.AddHook(TratarMensagem);

        var handle = new WindowInteropHelper(this).Handle;

        // Ctrl+Alt+L mostra e esconde sem tirar o foco da chamada — o usuário está no
        // meio de uma reunião e não pode perder o foco só para mexer na legenda.
        NativeMethods.RegisterHotKey(
            handle,
            IdAtalhoVisibilidade,
            NativeMethods.Modificadores.Control | NativeMethods.Modificadores.Alt | NativeMethods.Modificadores.NoRepeat,
            TeclaL);

        AplicarVisibilidadeEmCaptura();
    }

    /// <summary>Esconde ou revela a janela em compartilhamentos de tela.</summary>
    public void AplicarVisibilidadeEmCaptura()
    {
        var ok = NativeMethods.DefinirVisibilidadeEmCaptura(this, visivel: !settings.HideFromScreenCapture);

        // Se a chamada falhar, o usuário precisa saber: ele vai compartilhar a tela
        // achando que a legenda está escondida.
        if (!ok && settings.HideFromScreenCapture)
            DefinirStatus("atenção: não foi possível esconder o overlay de capturas de tela");
    }

    private IntPtr TratarMensagem(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado)
    {
        if (msg != MensagemHotKey || wParam.ToInt32() != IdAtalhoVisibilidade) return IntPtr.Zero;

        visivel = !visivel;
        Visibility = visivel ? Visibility.Visible : Visibility.Hidden;
        tratado = true;
        return IntPtr.Zero;
    }

    private void AoArrastar(object sender, MouseButtonEventArgs e)
    {
        // A janela não tem barra de título, então o arrasto acontece pelo corpo.
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void RestaurarPosicao()
    {
        if (settings.Left <= 0 && settings.Top <= 0)
        {
            PosicionarNoRodape();
            return;
        }

        Left = settings.Left;
        Top = settings.Top;

        // Monitor desconectado desde a última sessão deixaria a janela fora da tela,
        // invisível e sem como recuperar a não ser apagando a preferência na mão.
        if (!EstaVisivelEmAlgumMonitor()) PosicionarNoRodape();
    }

    private void PosicionarNoRodape()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = SystemParameters.PrimaryScreenHeight - 220;
    }

    private bool EstaVisivelEmAlgumMonitor() =>
        Left + Width > SystemParameters.VirtualScreenLeft
        && Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && Top + 80 > SystemParameters.VirtualScreenTop
        && Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    protected override void OnClosing(CancelEventArgs e)
    {
        settings.Left = Left;
        settings.Top = Top;
        settings.Width = Width;
        settings.Salvar();

        NativeMethods.UnregisterHotKey(new WindowInteropHelper(this).Handle, IdAtalhoVisibilidade);
        base.OnClosing(e);
    }
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Tlt.App.Overlay;

/// <summary>
/// Chamadas ao Windows que o WPF não expõe.
/// </summary>
internal static partial class NativeMethods
{
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    /// <summary>
    /// Esconde ou revela a janela para capturas de tela e compartilhamento.
    /// </summary>
    /// <remarks>
    /// Sem isto, compartilhar a tela numa reunião mostra a legenda para todos os
    /// participantes — inclusive a tradução do que eles acabaram de dizer. Exige
    /// Windows 10 build 19041, que já é o alvo do projeto.
    /// </remarks>
    public static bool DefinirVisibilidadeEmCaptura(Window janela, bool visivel)
    {
        var handle = new WindowInteropHelper(janela).Handle;
        if (handle == IntPtr.Zero) return false;

        return SetWindowDisplayAffinity(handle, visivel ? WdaNone : WdaExcludeFromCapture);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Modificadores de atalho global.</summary>
    public static class Modificadores
    {
        public const uint Alt = 0x0001;
        public const uint Control = 0x0002;
        public const uint Shift = 0x0004;

        /// <summary>Impede repetição enquanto a tecla fica pressionada.</summary>
        public const uint NoRepeat = 0x4000;
    }
}

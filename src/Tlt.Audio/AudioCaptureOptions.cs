using Tlt.Core.Audio;

namespace Tlt.Audio;

/// <summary>
/// Configuração da captura de áudio.
/// </summary>
public sealed class AudioCaptureOptions
{
    /// <summary>Formato entregue ao restante do pipeline.</summary>
    public AudioFormat TargetFormat { get; init; } = AudioFormat.Whisper;

    /// <summary>
    /// Quando verdadeiro, pede o formato alvo direto ao WASAPI em vez de converter
    /// aqui. O spike confirmou que o pedido é aceito, mas apenas com tom senoidal:
    /// não se sabe se a conversão interna preserva qualidade com fala.
    /// </summary>
    /// <remarks>
    /// Padrão falso, porque a conversão própria é testável e está sob nosso controle.
    /// Trocar depois de comparar a taxa de erro de transcrição nos dois caminhos.
    /// </remarks>
    public bool RequestFormatFromDevice { get; init; }

    /// <summary>
    /// Reabre a captura quando o fluxo termina sozinho, o que acontece quando o
    /// usuário troca a saída de som no meio da sessão.
    /// </summary>
    /// <remarks>
    /// Precisa ser feito por conta própria: o WithDefaultDeviceStreamRouting do NAudio
    /// segue o dispositivo de CAPTURA e é rejeitado junto com loopback. Sem esta
    /// reconexão, trocar o fone mata a legenda em silêncio, sem erro nenhum.
    /// </remarks>
    public bool ReconnectOnDeviceChange { get; init; } = true;

    /// <summary>Tentativas seguidas de reconexão antes de desistir.</summary>
    public int MaxReconnectAttempts { get; init; } = 5;

    /// <summary>Espera antes de cada tentativa de reconexão.</summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromMilliseconds(500);
}

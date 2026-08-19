namespace Tlt.Core.Audio;

/// <summary>
/// Fonte de áudio do pipeline. A implementação de produção captura o que o sistema
/// envia para a saída de som; implementações de teste leem de arquivo.
/// </summary>
public interface IAudioSource : IAsyncDisposable
{
    /// <summary>Formato entregue por esta fonte.</summary>
    AudioFormat Format { get; }

    /// <summary>Nome do dispositivo ativo, para diagnóstico na interface.</summary>
    string DeviceName { get; }

    /// <summary>
    /// Fluxo contínuo de áudio. Cada bloco chega fora da thread de áudio do driver,
    /// mas o corpo do consumidor ainda não pode demorar mais que o intervalo entre
    /// blocos — medido em ~10 ms na captura loopback do Windows.
    /// </summary>
    IAsyncEnumerable<AudioChunk> CaptureAsync(CancellationToken cancellationToken = default);
}

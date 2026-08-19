using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Tlt.Core.Audio;

namespace Tlt.Audio;

/// <summary>
/// Captura o áudio que o Windows está enviando para a saída de som: o que a outra
/// pessoa fala na chamada, e não o microfone.
/// </summary>
/// <remarks>
/// Usa WasapiRecorderBuilder. As classes WasapiLoopbackCapture e WasapiCapture estão
/// obsoletas no NAudio 3, apesar de a documentação oficial ainda ensiná-las.
/// </remarks>
public sealed class WasapiLoopbackSource : IAudioSource
{
    private readonly AudioCaptureOptions options;
    private WasapiRecorder recorder;
    private AudioNormalizer normalizer;
    private WaveFormat deviceFormat;
    private long quadrosEmitidos;

    private WasapiLoopbackSource(WasapiRecorder recorder, AudioCaptureOptions options)
    {
        this.recorder = recorder;
        this.options = options;
        deviceFormat = recorder.WaveFormat;
        normalizer = CriarNormalizer(deviceFormat, options);
    }

    /// <summary>
    /// Abre a captura no dispositivo de saída padrão.
    /// </summary>
    /// <remarks>
    /// Assíncrono porque abrir dispositivo de áudio é I/O, e esconder isso num
    /// construtor engana quem lê a chamada.
    /// </remarks>
    public static async Task<WasapiLoopbackSource> CreateAsync(
        AudioCaptureOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AudioCaptureOptions();
        var recorder = await AbrirAsync(options, cancellationToken).ConfigureAwait(false);
        return new WasapiLoopbackSource(recorder, options);
    }

    public AudioFormat Format => options.TargetFormat;

    public string DeviceName => recorder.DeviceFriendlyName;

    /// <summary>Formato bruto do dispositivo, antes da normalização.</summary>
    public AudioFormat DeviceFormat => new(deviceFormat.SampleRate, deviceFormat.Channels);

    /// <summary>
    /// Fluxo contínuo de áudio normalizado, atravessando trocas de dispositivo.
    /// </summary>
    public async IAsyncEnumerable<AudioChunk> CaptureAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tentativas = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var chunk in CapturarDoDispositivoAtual(cancellationToken).ConfigureAwait(false))
            {
                tentativas = 0;   // qualquer áudio recebido zera o histórico de falhas
                yield return chunk;
            }

            // Chegar aqui sem cancelamento significa que o fluxo acabou sozinho: o
            // dispositivo sumiu. Não é erro, é o usuário tirando o fone.
            if (cancellationToken.IsCancellationRequested || !options.ReconnectOnDeviceChange) yield break;
            if (++tentativas > options.MaxReconnectAttempts) yield break;

            await Task.Delay(options.ReconnectDelay, cancellationToken).ConfigureAwait(false);
            await ReconectarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<AudioChunk> CapturarDoDispositivoAtual(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var buffer in recorder.CaptureAsync(cancellationToken).ConfigureAwait(false))
        {
            var bytes = buffer.Data.Span;
            if (bytes.IsEmpty) continue;

            var amostrasBrutas = bytes.Length / (deviceFormat.BitsPerSample / 8);
            var brutas = ArrayPool<float>.Shared.Rent(amostrasBrutas);
            var convertidas = ArrayPool<float>.Shared.Rent(normalizer.MaxOutputSamples(amostrasBrutas));

            AudioChunk? chunk;
            try
            {
                LerAmostras(bytes, deviceFormat, brutas.AsSpan(0, amostrasBrutas));
                var escritas = normalizer.Process(brutas.AsSpan(0, amostrasBrutas), convertidas);

                if (escritas == 0)
                {
                    chunk = null;   // o reamostrador ainda acumula histórico
                }
                else
                {
                    var timestamp = TimeSpan.FromSeconds((double)quadrosEmitidos / options.TargetFormat.SampleRate);
                    quadrosEmitidos += escritas;

                    chunk = new AudioChunk(
                        convertidas.AsMemory(0, escritas).ToArray(),
                        timestamp,
                        HasDiscontinuity: (buffer.Flags & AudioClientBufferFlags.DataDiscontinuity) != 0);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(brutas);
                ArrayPool<float>.Shared.Return(convertidas);
            }

            if (chunk is not null) yield return chunk.Value;
        }
    }

    private async Task ReconectarAsync(CancellationToken cancellationToken)
    {
        await recorder.DisposeAsync().ConfigureAwait(false);
        recorder = await AbrirAsync(options, cancellationToken).ConfigureAwait(false);

        // O dispositivo novo pode ter taxa ou contagem de canais diferentes: placa
        // onboard a 48 kHz e headset a 44,1 kHz, por exemplo. O normalizador é
        // derivado do formato real, nunca de constante.
        deviceFormat = recorder.WaveFormat;
        normalizer = CriarNormalizer(deviceFormat, options);
    }

    private static async Task<WasapiRecorder> AbrirAsync(
        AudioCaptureOptions options,
        CancellationToken cancellationToken)
    {
        var builder = new WasapiRecorderBuilder()
            .WithLoopbackCapture()   // o que sai para o fone, não o microfone
            .WithEventSync();        // o driver avisa quando há dados, em vez de consultar

        // Nada de WithDefaultDeviceStreamRouting: ele segue o dispositivo de CAPTURA e
        // o NAudio rejeita a combinação com loopback. A reconexão fica em ReconectarAsync.

        if (options.RequestFormatFromDevice)
        {
            var alvo = options.TargetFormat;
            builder = builder.WithFormat(new WaveFormat(alvo.SampleRate, 16, alvo.Channels));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await builder.BuildAsync().ConfigureAwait(false);
    }

    private static AudioNormalizer CriarNormalizer(WaveFormat formato, AudioCaptureOptions options) =>
        new(new AudioFormat(formato.SampleRate, formato.Channels), options.TargetFormat);

    /// <summary>
    /// Converte as amostras cruas do dispositivo para ponto flutuante normalizado.
    /// </summary>
    /// <remarks>
    /// O WASAPI em modo compartilhado costuma entregar float de 32 bits, mas não é
    /// garantido, daí o tratamento de PCM de 16 bits.
    /// </remarks>
    internal static void LerAmostras(ReadOnlySpan<byte> bytes, WaveFormat formato, Span<float> destino)
    {
        switch (formato.BitsPerSample)
        {
            case 32:
                MemoryMarshal.Cast<byte, float>(bytes)[..destino.Length].CopyTo(destino);
                break;

            case 16:
                var pcm = MemoryMarshal.Cast<byte, short>(bytes);
                for (var i = 0; i < destino.Length; i++) destino[i] = pcm[i] / 32768f;
                break;

            default:
                throw new NotSupportedException(
                    $"Dispositivo entrega {formato.BitsPerSample} bits por amostra, e o pipeline trata 16 e 32.");
        }
    }

    public async ValueTask DisposeAsync() => await recorder.DisposeAsync().ConfigureAwait(false);
}

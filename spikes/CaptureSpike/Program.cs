using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

// Spike descartavel da task #2. Responde tres perguntas:
//   1. Que formato o Windows entrega na captura loopback DESTA maquina?
//   2. De que tamanho e com que frequencia vem cada buffer?
//   3. O que acontece quando nao ha audio tocando?
// E testa, de quebra, se da para pedir 16 kHz mono direto ao WASAPI — se der,
// o pipeline economiza a etapa inteira de resample.

var saida = Path.Combine(AppContext.BaseDirectory, "captura.wav");
const int segundosDeCaptura = 30;

Console.WriteLine("=== TLT | spike de captura loopback (task #2) ===");
Console.WriteLine();

await using var recorder = new WasapiRecorderBuilder()
    .WithLoopbackCapture()  // captura o que SAI para o fone, nao o microfone
    .WithEventSync()        // o driver avisa quando ha dados, em vez de ficar consultando
    .Build();

var formato = recorder.WaveFormat;

Console.WriteLine($"Dispositivo  : {recorder.DeviceFriendlyName}");
Console.WriteLine($"Latencia     : {recorder.LatencyMilliseconds} ms");
Console.WriteLine($"Low latency  : {recorder.LowLatencyActive}" +
                  (recorder.LowLatencyActive ? "" : $"  (motivo: {recorder.LowLatencyUnavailableReason})"));
Console.WriteLine();
Console.WriteLine("--- formato entregue pelo Windows ---");
Console.WriteLine($"  {formato.SampleRate} Hz | {formato.Channels} canal(is) | {formato.BitsPerSample} bits | {formato.Encoding}");
Console.WriteLine($"  {formato.AverageBytesPerSecond:N0} bytes/s");
Console.WriteLine("  O Whisper quer 16000 Hz mono: a diferenca acima e o trabalho do pipeline.");
Console.WriteLine();

using var writer = new WaveFileWriter(saida, formato);
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(segundosDeCaptura));

long totalBytes = 0;
var buffers = 0;
var silenciosos = 0;
var descontinuidades = 0;
var errosTimestamp = 0;
var menor = int.MaxValue;
var maior = 0;
var maiorIntervalo = TimeSpan.Zero;
var ultimo = TimeSpan.Zero;
var relogio = Stopwatch.StartNew();

Console.WriteLine($"Capturando por {segundosDeCaptura}s. Deixe um video tocando AGORA.");
Console.WriteLine("Pause o video por uns 5s no meio da captura: e parte da medicao.");
Console.WriteLine();

try
{
    // IAsyncEnumerable: cada buffer chega aqui ja fora da thread de audio do
    // driver, o que torna natural o desacoplamento que o pipeline de producao
    // precisa. Ver Docs/Arquitetura/Pipeline de Audio.md
    await foreach (var buffer in recorder.CaptureAsync(cts.Token))
    {
        var dados = buffer.Data.Span;
        writer.Write(dados);

        var agora = relogio.Elapsed;
        var intervalo = agora - ultimo;
        if (buffers > 0)
        {
            if (intervalo > maiorIntervalo) maiorIntervalo = intervalo;
            if (intervalo.TotalMilliseconds > 700)
                Console.WriteLine($"  [{agora.TotalSeconds,5:F1}s] {intervalo.TotalMilliseconds:N0} ms sem nenhum buffer");
        }
        ultimo = agora;

        buffers++;
        totalBytes += dados.Length;
        if (dados.Length < menor) menor = dados.Length;
        if (dados.Length > maior) maior = dados.Length;

        // O WASAPI marca cada buffer. Silent economiza processamento;
        // DataDiscontinuity denuncia audio perdido — o sintoma que a doc avisa
        // que aparece quando alguem trava a thread de captura.
        if ((buffer.Flags & AudioClientBufferFlags.Silent) != 0) silenciosos++;
        if ((buffer.Flags & AudioClientBufferFlags.DataDiscontinuity) != 0) descontinuidades++;
        if ((buffer.Flags & AudioClientBufferFlags.TimestampError) != 0) errosTimestamp++;
    }
}
catch (OperationCanceledException)
{
    // fim normal: os 30 segundos acabaram
}

Console.WriteLine();
Console.WriteLine("=== RESULTADO ===");
Console.WriteLine($"  Buffers recebidos   : {buffers:N0}");
Console.WriteLine($"  Bytes capturados    : {totalBytes:N0}");

if (buffers == 0)
{
    Console.WriteLine();
    Console.WriteLine("  NENHUM dado chegou. Provavelmente nao havia audio tocando, ou o");
    Console.WriteLine("  dispositivo de saida padrao nao e o que estava reproduzindo.");
}
else
{
    var media = totalBytes / (double)buffers;
    Console.WriteLine($"  Buffer menor/maior  : {menor:N0} / {maior:N0} bytes");
    Console.WriteLine($"  Buffer medio        : {media:N0} bytes (~{media / formato.AverageBytesPerSecond * 1000:N1} ms de audio)");
    Console.WriteLine($"  Maior intervalo     : {maiorIntervalo.TotalMilliseconds:N0} ms entre buffers");
    Console.WriteLine($"  Audio gravado       : {totalBytes / (double)formato.AverageBytesPerSecond:N1} s");
    Console.WriteLine($"  Buffers silenciosos : {silenciosos:N0}  (flag Silent)");
    Console.WriteLine($"  Descontinuidades    : {descontinuidades:N0}  (flag DataDiscontinuity — audio perdido)");
    Console.WriteLine($"  Erros de timestamp  : {errosTimestamp:N0}");
}

Console.WriteLine($"  Arquivo             : {saida}");

Console.WriteLine();
Console.WriteLine("--- da para pedir 16 kHz mono direto ao WASAPI? ---");
try
{
    await using var teste = new WasapiRecorderBuilder()
        .WithLoopbackCapture()
        .WithFormat(new WaveFormat(16000, 16, 1))
        .Build();

    var f = teste.WaveFormat;
    if (f.SampleRate == 16000 && f.Channels == 1)
        Console.WriteLine($"  SIM: {f.SampleRate} Hz mono. O pipeline pode dispensar downmix e resample.");
    else
        Console.WriteLine($"  NAO: pedi 16000/1 e veio {f.SampleRate}/{f.Channels}. O pipeline converte.");
}
catch (Exception ex)
{
    Console.WriteLine($"  NAO: {ex.GetType().Name} - {ex.Message}");
    Console.WriteLine("  O pipeline precisa fazer downmix + resample por conta propria.");
}

Console.WriteLine();
Console.WriteLine("Ouca o .wav para confirmar que capturou o som certo.");

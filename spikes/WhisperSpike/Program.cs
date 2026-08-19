using System.Diagnostics;
using System.Text;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

// Spike da task #3: mede o RTF (real-time factor) do Whisper local.
//
// A arquitetura escolhida (janela deslizante, ver Docs/Arquitetura/Pipeline de
// Audio.md) reprocessa ~10s de audio a cada ~800ms, o que exige RTF >= 12.
// Este spike descobre qual modelo alcanca isso em qual hardware.

// Sem isto o Whisper.net cai em CPU mesmo com as DLLs de CUDA presentes.
RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cuda12, RuntimeLibrary.Cuda, RuntimeLibrary.Cpu];

var baseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
var audioPath = Path.GetFullPath(Path.Combine(baseDir, "audio", "fala-en.wav"));
var referenciaPath = Path.GetFullPath(Path.Combine(baseDir, "audio", "referencia.txt"));
var modelsDir = Path.GetFullPath(Path.Combine(baseDir, "models"));
Directory.CreateDirectory(modelsDir);

// Modelos a medir: passe por argumento (tiny base small medium turbo) ou use o padrao.
var pedidos = args.Length > 0 ? args : ["base", "small"];
var mapa = new Dictionary<string, GgmlType>(StringComparer.OrdinalIgnoreCase)
{
    ["tiny"] = GgmlType.Tiny, ["base"] = GgmlType.Base, ["small"] = GgmlType.Small,
    ["medium"] = GgmlType.Medium, ["turbo"] = GgmlType.LargeV3Turbo, ["large"] = GgmlType.LargeV3
};
const QuantizationType quantizacao = QuantizationType.Q5_0;

// --- carrega o audio inteiro em memoria, para nao medir I/O junto ---
float[] amostras;
TimeSpan duracaoAudio;
using (var reader = new AudioFileReader(audioPath))
{
    duracaoAudio = reader.TotalTime;
    var lista = new List<float>((int)(reader.Length / 2));
    var buf = new float[16000];
    int lidos;
    while ((lidos = reader.Read(buf)) > 0) lista.AddRange(buf.AsSpan(0, lidos).ToArray());
    amostras = lista.ToArray();
}

var referencia = File.ReadAllText(referenciaPath);

Console.WriteLine("=== TLT | spike de RTF do Whisper (task #3) ===");
Console.WriteLine();
Console.WriteLine($"Audio      : {Path.GetFileName(audioPath)} | {duracaoAudio.TotalSeconds:F1}s | {amostras.Length:N0} amostras");
Console.WriteLine($"Quantizacao: {quantizacao}");
Console.WriteLine($"Alvo       : RTF >= 12 para a janela deslizante");
Console.WriteLine();

var resultados = new List<(string Modelo, double Mb, double Rtf, double Segundos, double Wer, string Texto)>();

foreach (var nome in pedidos)
{
    if (!mapa.TryGetValue(nome, out var tipo)) { Console.WriteLine($"[?] modelo desconhecido: {nome}"); continue; }

    var caminho = Path.Combine(modelsDir, $"ggml-{tipo}-{quantizacao}.bin");
    if (!File.Exists(caminho))
    {
        Console.Write($"[{nome}] baixando... ");
        await using var origem = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(tipo, quantizacao);
        await using var destino = File.Create(caminho);
        await origem.CopyToAsync(destino);
        Console.WriteLine("ok");
    }

    var mb = new FileInfo(caminho).Length / 1024.0 / 1024.0;
    Console.Write($"[{nome}] {mb:N0} MB | carregando... ");

    var relogioCarga = Stopwatch.StartNew();
    using var factory = WhisperFactory.FromPath(caminho, new WhisperFactoryOptions { UseGpu = true });
    relogioCarga.Stop();

    await using var processor = factory.CreateBuilder()
        .WithLanguage("en")   // fixo: mais rapido e mais confiavel que auto-deteccao
        .Build();

    Console.Write($"carregou em {relogioCarga.Elapsed.TotalSeconds:F1}s | aquecendo... ");

    // A primeira passada paga a inicializacao da GPU — no Vulkan isso inclui
    // compilar shaders. Sem descartar essa passada, o primeiro modelo medido
    // aparece absurdamente mais lento que os seguintes.
    await foreach (var _ in processor.ProcessAsync(amostras.AsMemory(0, Math.Min(16000 * 3, amostras.Length)))) { }

    Console.Write("transcrevendo... ");

    var texto = new StringBuilder();
    var relogio = Stopwatch.StartNew();
    await foreach (var seg in processor.ProcessAsync(amostras))
        texto.Append(seg.Text);
    relogio.Stop();

    var rtf = duracaoAudio.TotalSeconds / relogio.Elapsed.TotalSeconds;
    var transcrito = texto.ToString().Trim();
    var wer = CalcularWer(referencia, transcrito);

    Console.WriteLine($"{relogio.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"        RTF {rtf:F1}x | WER {wer:P1} | {(rtf >= 12 ? "SUSTENTA janela deslizante" : rtf >= 2 ? "so segmentacao por frase" : "inviavel para tempo real")}");
    Console.WriteLine();

    resultados.Add((nome, mb, rtf, relogio.Elapsed.TotalSeconds, wer, transcrito));
}

Console.WriteLine();
Console.WriteLine("=== RESUMO ===");
Console.WriteLine($"  biblioteca carregada: {RuntimeOptions.LoadedLibrary}");
Console.WriteLine($"  runtime: {WhisperFactory.GetRuntimeInfo()}");
Console.WriteLine();
Console.WriteLine($"  {"modelo",-8} {"MB",6} {"tempo",8} {"RTF",7} {"WER",7}   veredito");
foreach (var r in resultados)
    Console.WriteLine($"  {r.Modelo,-8} {r.Mb,6:N0} {r.Segundos,7:F1}s {r.Rtf,6:F1}x {r.Wer,6:P0}   {(r.Rtf >= 12 ? "janela deslizante" : r.Rtf >= 2 ? "segmentacao por frase" : "inviavel")}");

Console.WriteLine();
foreach (var r in resultados)
{
    Console.WriteLine($"--- transcricao [{r.Modelo}] ---");
    Console.WriteLine($"  {r.Texto}");
    Console.WriteLine();
}

// WER por distancia de edicao em palavras. Normaliza caixa e pontuacao, porque
// o Whisper pontua diferente e isso nao e erro de reconhecimento.
static double CalcularWer(string referencia, string hipotese)
{
    string[] Normalizar(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? char.ToLowerInvariant(c) : ' ').ToArray())
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    var r = Normalizar(referencia);
    var h = Normalizar(hipotese);
    if (r.Length == 0) return 0;

    var d = new int[r.Length + 1, h.Length + 1];
    for (var i = 0; i <= r.Length; i++) d[i, 0] = i;
    for (var j = 0; j <= h.Length; j++) d[0, j] = j;
    for (var i = 1; i <= r.Length; i++)
        for (var j = 1; j <= h.Length; j++)
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                               d[i - 1, j - 1] + (r[i - 1] == h[j - 1] ? 0 : 1));
    return d[r.Length, h.Length] / (double)r.Length;
}

using System.Diagnostics;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

// Spike da task #13: a traducao consegue rodar LOCAL com qualidade e latencia
// aceitaveis? Sem isso a promessa de privacidade do ADR nao fecha, porque o texto
// transcrito — que E o conteudo da conversa — iria para uma API de terceiro.
//
// Mede por FRASE, nao pelo texto inteiro: no uso real a traducao acontece a cada
// segmento confirmado pela janela deslizante.

var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var modelPath = Path.Combine(baseDir, "models", "qwen2.5-3b-instruct-q4km.gguf");
var referenciaPath = Path.GetFullPath(Path.Combine(baseDir, "..", "WhisperSpike", "audio", "referencia.txt"));

if (!File.Exists(modelPath)) { Console.WriteLine($"modelo nao encontrado: {modelPath}"); return; }

Console.WriteLine("=== TLT | spike de traducao local (task #13) ===");
Console.WriteLine();

// Vulkan e o mesmo backend do Whisper: um so caminho de GPU no produto inteiro,
// sem CUDA Toolkit e servindo NVIDIA/AMD/Intel.
NativeLibraryConfig.All.WithLogCallback((level, msg) => { });

var parametros = new ModelParams(modelPath)
{
    ContextSize = 2048,
    GpuLayerCount = 99,   // tudo que couber na GPU
};

var relogioCarga = Stopwatch.StartNew();
using var weights = await LLamaWeights.LoadFromFileAsync(parametros);
relogioCarga.Stop();

Console.WriteLine($"Modelo   : {Path.GetFileName(modelPath)}");
Console.WriteLine($"Tamanho  : {weights.SizeInBytes / 1024.0 / 1024.0:N0} MB | {weights.ParameterCount / 1_000_000_000.0:N1}B parametros");
Console.WriteLine($"Carga    : {relogioCarga.Elapsed.TotalSeconds:F1}s");
Console.WriteLine();

var executor = new StatelessExecutor(weights, parametros);

// Frases do mesmo texto usado no spike de STT, para comparar o pipeline ponta a ponta.
var texto = File.ReadAllText(referenciaPath);
var frases = texto.Split(['.', '?'], StringSplitOptions.RemoveEmptyEntries)
                  .Select(f => f.Trim())
                  .Where(f => f.Length > 15)
                  .ToList();

Console.WriteLine($"Traduzindo {frases.Count} frases EN->PT, uma a uma.");
Console.WriteLine();

// Aquecimento: a primeira inferencia paga inicializacao de GPU, como no Whisper.
await Consumir(executor.InferAsync(Prompt("Hello.", []), Params()));

var tempos = new List<double>();
var historico = new List<string>();

foreach (var frase in frases)
{
    var relogio = Stopwatch.StartNew();
    var traducao = await Consumir(executor.InferAsync(Prompt(frase, historico), Params()));
    relogio.Stop();

    tempos.Add(relogio.Elapsed.TotalMilliseconds);
    historico.Add(traducao);
    if (historico.Count > 3) historico.RemoveAt(0);

    Console.WriteLine($"[{relogio.Elapsed.TotalMilliseconds,6:N0} ms] {frase}");
    Console.WriteLine($"           -> {traducao}");
    Console.WriteLine();
}

Console.WriteLine("=== RESUMO ===");
Console.WriteLine($"  frases          : {tempos.Count}");
Console.WriteLine($"  latencia media  : {tempos.Average():N0} ms");
Console.WriteLine($"  mediana         : {tempos.Order().ElementAt(tempos.Count / 2):N0} ms");
Console.WriteLine($"  minima / maxima : {tempos.Min():N0} / {tempos.Max():N0} ms");
Console.WriteLine();
Console.WriteLine("  A traducao entra DEPOIS do STT, entao soma na latencia total.");
Console.WriteLine("  Com o alvo de 1,5-3s ponta a ponta, sobra pouco para esta etapa.");

return;

// Formato ChatML do Qwen. O historico das ultimas frases entra como contexto:
// e o que mantem terminologia e resolve pronomes em reuniao tecnica.
string Prompt(string frase, List<string> anteriores)
{
    var sb = new StringBuilder();
    sb.Append("<|im_start|>system\n");
    sb.Append("Você é um tradutor de inglês para português brasileiro em uma reunião de trabalho. ");
    sb.Append("Traduza APENAS a frase do usuário, sem explicar, sem comentar, sem repetir o original. ");
    sb.Append("Mantenha termos técnicos e nomes de produto em inglês.");
    if (anteriores.Count > 0)
        sb.Append($" Contexto das frases anteriores já traduzidas: {string.Join(" ", anteriores)}");
    sb.Append("<|im_end|>\n<|im_start|>user\n");
    sb.Append(frase);
    sb.Append("<|im_end|>\n<|im_start|>assistant\n");
    return sb.ToString();
}

InferenceParams Params() => new()
{
    MaxTokens = 200,
    AntiPrompts = ["<|im_end|>", "<|im_start|>"],
    SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.2f }  // traducao quer previsibilidade
};

async Task<string> Consumir(IAsyncEnumerable<string> fluxo)
{
    var sb = new StringBuilder();
    await foreach (var t in fluxo) sb.Append(t);
    return sb.ToString().Replace("<|im_end|>", "").Trim();
}

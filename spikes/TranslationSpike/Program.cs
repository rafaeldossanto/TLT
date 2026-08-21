using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

// Spike da task #14: Opus-MT (Marian) em ONNX, com cache de key/value no decoder.
//
// Sem cache o decoder reprocessa a sequencia inteira a cada token gerado — O(n²) —
// e a media ficou em 1.709 ms. Com cache cada passo processa apenas o token novo.

var dir = @"C:\Users\rafae\Work\TLT\spikes\TranslationSpike\models\opus-mt-en-pt";
const int TokenInicioDecoder = 54775;
const int TokenFim = 44670;
const int MaxTokens = 256;
const int Camadas = 6;

var vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(dir, "vocab.json")))!;
var vocabInverso = new Dictionary<int, string>(vocab.Count);
foreach (var (token, id) in vocab) vocabInverso[id] = token;
var idDesconhecido = vocab.GetValueOrDefault("<unk>", 52024);

using var spmStream = File.OpenRead(Path.Combine(dir, "source.spm"));
var spm = SentencePieceTokenizer.Create(spmStream, false, false);

Console.Write("carregando sessoes ONNX... ");
var relogio = Stopwatch.StartNew();
using var encoder = new InferenceSession(Path.Combine(dir, "encoder_model_quantized.onnx"));
using var decoder = new InferenceSession(Path.Combine(dir, "decoder_model_quantized.onnx"));
using var decoderComCache = new InferenceSession(Path.Combine(dir, "decoder_with_past_model_quantized.onnx"));
Console.WriteLine($"{relogio.Elapsed.TotalSeconds:F1}s");
Console.WriteLine();

Traduzir("Hello.");   // aquecimento

var texto = File.ReadAllText(@"C:\Users\rafae\Work\TLT\spikes\WhisperSpike\audio\referencia.txt");
var frases = texto.Split(['.', '?'], StringSplitOptions.RemoveEmptyEntries)
                  .Select(f => f.Trim()).Where(f => f.Length > 15).ToList();

var latencias = new List<double>();

foreach (var frase in frases)
{
    var t = Stopwatch.StartNew();
    var traducao = Traduzir(frase);
    t.Stop();
    latencias.Add(t.Elapsed.TotalMilliseconds);

    Console.WriteLine($"[{t.Elapsed.TotalMilliseconds,6:N0} ms] {traducao}");
}

Console.WriteLine();
Console.WriteLine("=== RESUMO (com cache de key/value) ===");
Console.WriteLine($"  latencia media  : {latencias.Average():N0} ms");
Console.WriteLine($"  mediana         : {latencias.Order().ElementAt(latencias.Count / 2):N0} ms");
Console.WriteLine($"  minima / maxima : {latencias.Min():N0} / {latencias.Max():N0} ms");
Console.WriteLine();
Console.WriteLine($"  alvo            : abaixo de 500 ms");
Console.WriteLine($"  sem cache       : 1.709 ms");
Console.WriteLine($"  Qwen2.5-3B      : 2.232 ms (descartado)");

string Traduzir(string entrada)
{
    var pecas = spm.EncodeToTokens(entrada, out _, considerNormalization: true);
    var ids = new List<long>(pecas.Count + 1);
    foreach (var p in pecas) ids.Add(vocab.TryGetValue(p.Value, out var id) ? id : idDesconhecido);
    ids.Add(TokenFim);

    var comprimento = ids.Count;
    var inputIds = new DenseTensor<long>([1, comprimento]);
    var attention = new DenseTensor<long>([1, comprimento]);
    for (var i = 0; i < comprimento; i++) { inputIds[0, i] = ids[i]; attention[0, i] = 1; }

    using var saidaEncoder = encoder.Run([
        NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
        NamedOnnxValue.CreateFromTensor("attention_mask", attention),
    ]);
    var hidden = saidaEncoder.First(v => v.Name == "last_hidden_state").AsTensor<float>().ToDenseTensor();

    // --- primeira passada: sem cache, produz os dois caches ---
    var primeiroId = new DenseTensor<long>([1, 1]);
    primeiroId[0, 0] = TokenInicioDecoder;

    using var primeira = decoder.Run([
        NamedOnnxValue.CreateFromTensor("input_ids", primeiroId),
        NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hidden),
        NamedOnnxValue.CreateFromTensor("encoder_attention_mask", attention),
    ]);

    var gerados = new List<long>();
    var proximo = Argmax(primeira.First(v => v.Name == "logits").AsTensor<float>());

    var cacheDecoder = new DenseTensor<float>[Camadas * 2];
    var cacheEncoder = new DenseTensor<float>[Camadas * 2];
    for (var c = 0; c < Camadas; c++)
    {
        cacheDecoder[c * 2] = Copiar(primeira, $"present.{c}.decoder.key");
        cacheDecoder[c * 2 + 1] = Copiar(primeira, $"present.{c}.decoder.value");
        // O cache do encoder nao muda entre passos: e calculado uma vez e reusado.
        cacheEncoder[c * 2] = Copiar(primeira, $"present.{c}.encoder.key");
        cacheEncoder[c * 2 + 1] = Copiar(primeira, $"present.{c}.encoder.value");
    }

    // --- passos seguintes: so o token novo entra ---
    for (var passo = 0; passo < MaxTokens && proximo != TokenFim; passo++)
    {
        gerados.Add(proximo);

        var tokenAtual = new DenseTensor<long>([1, 1]);
        tokenAtual[0, 0] = proximo;

        var entradas = new List<NamedOnnxValue>(26)
        {
            NamedOnnxValue.CreateFromTensor("input_ids", tokenAtual),
            NamedOnnxValue.CreateFromTensor("encoder_attention_mask", attention),
        };

        for (var c = 0; c < Camadas; c++)
        {
            entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.decoder.key", cacheDecoder[c * 2]));
            entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.decoder.value", cacheDecoder[c * 2 + 1]));
            entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.encoder.key", cacheEncoder[c * 2]));
            entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.encoder.value", cacheEncoder[c * 2 + 1]));
        }

        using var saida = decoderComCache.Run(entradas);
        proximo = Argmax(saida.First(v => v.Name == "logits").AsTensor<float>());

        for (var c = 0; c < Camadas; c++)
        {
            cacheDecoder[c * 2] = Copiar(saida, $"present.{c}.decoder.key");
            cacheDecoder[c * 2 + 1] = Copiar(saida, $"present.{c}.decoder.value");
        }
    }

    var sb = new StringBuilder();
    foreach (var id in gerados)
        if (vocabInverso.TryGetValue((int)id, out var token))
            sb.Append(token.Replace("\u2581", " "));

    return sb.ToString().Trim();
}

static DenseTensor<float> Copiar(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> saida, string nome) =>
    saida.First(v => v.Name == nome).AsTensor<float>().ToDenseTensor();

// O indexador de Tensor recalcula o deslocamento a cada acesso. Com 54.776
// posicoes por token gerado, isso domina o tempo. O buffer e contiguo, entao
// vale percorrer o span direto.
static long Argmax(Tensor<float> logits)
{
    var span = logits.ToDenseTensor().Buffer.Span;
    var melhor = 0;
    var melhorValor = float.MinValue;
    for (var v = 0; v < span.Length; v++)
    {
        if (span[v] <= melhorValor) continue;
        melhorValor = span[v];
        melhor = v;
    }
    return melhor;
}

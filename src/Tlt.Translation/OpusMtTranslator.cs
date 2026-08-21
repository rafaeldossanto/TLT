using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tlt.Core.Models;
using Tlt.Core.Transcription;
using Tlt.Core.Translation;

namespace Tlt.Translation;

/// <summary>
/// Traduz na própria máquina, com um modelo Opus-MT (Marian) rodando em ONNX.
/// </summary>
/// <remarks>
/// É o que fecha a promessa de privacidade: sem tradução local, o texto transcrito —
/// que é o conteúdo da conversa — iria para uma API de terceiro, e anunciar que o
/// áudio não sai da máquina seria meia-verdade.
///
/// Roda em CPU, deixando a GPU para o reconhecedor.
/// </remarks>
public sealed class OpusMtTranslator : ITranslator
{
    private readonly OpusMtOptions options;
    private readonly MarianTokenizer tokenizer;
    private readonly InferenceSession encoder;
    private readonly InferenceSession decoder;
    private readonly InferenceSession decoderComCache;
    private readonly int camadas;
    private readonly Dictionary<string, string> cache = [];
    private readonly Lock trava = new();

    private OpusMtTranslator(
        OpusMtOptions options,
        MarianTokenizer tokenizer,
        InferenceSession encoder,
        InferenceSession decoder,
        InferenceSession decoderComCache)
    {
        this.options = options;
        this.tokenizer = tokenizer;
        this.encoder = encoder;
        this.decoder = decoder;
        this.decoderComCache = decoderComCache;

        // Derivado do próprio modelo, em vez de fixo no código: trocar para outro par
        // de idiomas não deve exigir mexer aqui.
        camadas = decoderComCache.InputMetadata.Keys.Count(k => k.EndsWith(".decoder.key"));
    }

    /// <inheritdoc />
    public bool RunsLocally => true;

    /// <summary>Carrega o modelo, baixando na primeira vez.</summary>
    public static async Task<OpusMtTranslator> CreateAsync(
        OpusMtOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OpusMtOptions();

        string[] arquivos =
        [
            "encoder_model_quantized.onnx",
            "decoder_model_quantized.onnx",
            "decoder_with_past_model_quantized.onnx",
            "source.spm",
            "vocab.json",
            "config.json",
        ];

        var caminhos = new Dictionary<string, string>();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        foreach (var arquivo in arquivos)
        {
            caminhos[arquivo] = await ModelCache.GetOrDownloadAsync(
                options.CacheDirectory,
                arquivo,
                ct => http.GetStreamAsync($"https://huggingface.co/{options.Repositorio}/resolve/main/{arquivo}", ct),
                cancellationToken).ConfigureAwait(false);
        }

        var json = await File.ReadAllTextAsync(caminhos["config.json"], cancellationToken).ConfigureAwait(false);
        var config = JsonDocument.Parse(json).RootElement;

        var tokenizer = MarianTokenizer.Carregar(
            caminhos["source.spm"],
            caminhos["vocab.json"],
            idFim: config.GetProperty("eos_token_id").GetInt32(),
            idInicioDecodificador: config.GetProperty("decoder_start_token_id").GetInt32());

        // Limita os núcleos da tradução para não sufocar o reconhecedor, que tem prazo
        // apertado e depende de CPU para alimentar a GPU.
        var sessao = new SessionOptions();
        if (options.MaxThreads > 0)
        {
            sessao.IntraOpNumThreads = options.MaxThreads;
            sessao.InterOpNumThreads = 1;
        }

        return new OpusMtTranslator(
            options,
            tokenizer,
            new InferenceSession(caminhos["encoder_model_quantized.onnx"], sessao),
            new InferenceSession(caminhos["decoder_model_quantized.onnx"], sessao),
            new InferenceSession(caminhos["decoder_with_past_model_quantized.onnx"], sessao));
    }

    /// <inheritdoc />
    /// <remarks>
    /// O parâmetro de contexto é ignorado. Opus-MT traduz frase a frase e não aceita
    /// histórico da conversa como um LLM aceitaria; a interface mantém o parâmetro
    /// porque um tradutor remoto baseado em LLM saberia usá-lo.
    /// </remarks>
    public Task<TranslatedSegment> TranslateAsync(
        TranscriptSegment segment,
        IReadOnlyList<string> context,
        CancellationToken cancellationToken = default)
    {
        var traducao = Traduzir(segment.Text, cancellationToken);
        return Task.FromResult(new TranslatedSegment(segment, traducao, options.TargetLanguage));
    }

    private string Traduzir(string texto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        lock (trava)
        {
            if (cache.TryGetValue(texto, out var guardada)) return guardada;
        }

        var (protegido, marcadores) = GlossaryProtection.Proteger(texto, options.Glossary);
        var traduzido = GlossaryProtection.Restaurar(Executar(protegido, cancellationToken), marcadores);

        lock (trava)
        {
            // Limpeza grosseira em vez de LRU: o cache existe para saudações e
            // confirmações repetidas, e a complexidade de um LRU não se paga aqui.
            if (cache.Count >= options.CacheSize) cache.Clear();
            cache[texto] = traduzido;
        }

        return traduzido;
    }

    private string Executar(string texto, CancellationToken cancellationToken)
    {
        var ids = tokenizer.Codificar(texto);

        var inputIds = new DenseTensor<long>([1, ids.Length]);
        var attention = new DenseTensor<long>([1, ids.Length]);
        for (var i = 0; i < ids.Length; i++)
        {
            inputIds[0, i] = ids[i];
            attention[0, i] = 1;
        }

        using var saidaEncoder = encoder.Run([
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
        ]);
        var hidden = saidaEncoder.First(v => v.Name == "last_hidden_state").AsTensor<float>().ToDenseTensor();

        var primeiroId = new DenseTensor<long>([1, 1]);
        primeiroId[0, 0] = tokenizer.IdInicioDecodificador;

        using var primeira = decoder.Run([
            NamedOnnxValue.CreateFromTensor("input_ids", primeiroId),
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hidden),
            NamedOnnxValue.CreateFromTensor("encoder_attention_mask", attention),
        ]);

        var proximo = Argmax(primeira.First(v => v.Name == "logits").AsTensor<float>());

        var cacheDecoder = new DenseTensor<float>[camadas * 2];
        var cacheEncoder = new DenseTensor<float>[camadas * 2];
        for (var c = 0; c < camadas; c++)
        {
            cacheDecoder[c * 2] = Copiar(primeira, $"present.{c}.decoder.key");
            cacheDecoder[c * 2 + 1] = Copiar(primeira, $"present.{c}.decoder.value");

            // O cache do encoder não muda entre passos: é calculado uma vez e reusado.
            cacheEncoder[c * 2] = Copiar(primeira, $"present.{c}.encoder.key");
            cacheEncoder[c * 2 + 1] = Copiar(primeira, $"present.{c}.encoder.value");
        }

        var gerados = new List<long>(options.MaxTokens);

        for (var passo = 0; passo < options.MaxTokens && proximo != tokenizer.IdFimDeSequencia; passo++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            gerados.Add(proximo);

            var tokenAtual = new DenseTensor<long>([1, 1]);
            tokenAtual[0, 0] = proximo;

            var entradas = new List<NamedOnnxValue>(2 + camadas * 4)
            {
                NamedOnnxValue.CreateFromTensor("input_ids", tokenAtual),
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", attention),
            };

            for (var c = 0; c < camadas; c++)
            {
                entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.decoder.key", cacheDecoder[c * 2]));
                entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.decoder.value", cacheDecoder[c * 2 + 1]));
                entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.encoder.key", cacheEncoder[c * 2]));
                entradas.Add(NamedOnnxValue.CreateFromTensor($"past_key_values.{c}.encoder.value", cacheEncoder[c * 2 + 1]));
            }

            using var saida = decoderComCache.Run(entradas);
            proximo = Argmax(saida.First(v => v.Name == "logits").AsTensor<float>());

            for (var c = 0; c < camadas; c++)
            {
                cacheDecoder[c * 2] = Copiar(saida, $"present.{c}.decoder.key");
                cacheDecoder[c * 2 + 1] = Copiar(saida, $"present.{c}.decoder.value");
            }
        }

        return tokenizer.Decodificar(gerados);
    }

    private static DenseTensor<float> Copiar(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> saida, string nome) =>
        saida.First(v => v.Name == nome).AsTensor<float>().ToDenseTensor();

    /// <summary>Índice do maior logit.</summary>
    /// <remarks>
    /// Percorre o buffer contíguo e não o indexador do tensor: são dezenas de milhares
    /// de posições por token gerado, e o indexador recalcula o deslocamento a cada
    /// acesso. Trocar isso derrubou a latência de 1.307 ms para 1.013 ms por frase.
    /// </remarks>
    private static long Argmax(Tensor<float> logits)
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

    public ValueTask DisposeAsync()
    {
        encoder.Dispose();
        decoder.Dispose();
        decoderComCache.Dispose();
        return ValueTask.CompletedTask;
    }
}

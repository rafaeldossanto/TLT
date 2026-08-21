using Tlt.Core.Models;

namespace Tlt.Translation;

/// <summary>
/// Configuração do tradutor local Opus-MT.
/// </summary>
public sealed class OpusMtOptions
{
    /// <summary>Repositório HuggingFace de onde o modelo é baixado.</summary>
    /// <remarks>
    /// Encoder-decoder Marian quantizado em int8. Escolhido por medição: contra um LLM
    /// generalista de 3B ficou mais rápido e, principalmente, sem os erros que
    /// invertiam o sentido das frases.
    /// </remarks>
    public string Repositorio { get; init; } = "R4kSo1997/opus-mt-en-pt-onnx-int8";

    /// <summary>Diretório de cache dos modelos.</summary>
    public string CacheDirectory { get; init; } = Path.Combine(ModelCache.DefaultDirectory, "opus-mt-en-pt");

    /// <summary>Idioma de destino, para rotular o resultado.</summary>
    public string TargetLanguage { get; init; } = "pt";

    /// <summary>Teto de tokens gerados por frase.</summary>
    public int MaxTokens { get; init; } = 256;

    /// <summary>Quantas traduções ficam em cache.</summary>
    /// <remarks>
    /// Em reunião, saudações e confirmações curtas se repetem muito, e cada acerto de
    /// cache economiza cerca de um segundo.
    /// </remarks>
    public int CacheSize { get; init; } = 500;

    /// <summary>
    /// Termos que devem sair iguais na tradução: nomes de produto, siglas internas,
    /// jargão da empresa.
    /// </summary>
    /// <remarks>
    /// Nada destrói mais rápido a confiança do usuário do que ver o nome do próprio
    /// produto traduzido no meio da legenda.
    /// </remarks>
    public IReadOnlyList<string> Glossary { get; init; } = [];
}

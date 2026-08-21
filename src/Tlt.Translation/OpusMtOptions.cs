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

    /// <summary>Núcleos que a tradução pode usar. Zero deixa o padrão da biblioteca.</summary>
    /// <remarks>
    /// Medido em 18/08/2026: com o tradutor usando todos os núcleos, o custo de uma
    /// passada do reconhecedor saltou de 1.214 ms para 2.811 ms — mais que o dobro.
    /// O gargalo compartilhado é **CPU**, não GPU: o reconhecedor roda em Vulkan, mas
    /// depende de CPU para alimentar a GPU, e um tradutor que ocupa tudo o deixa sem
    /// margem.
    ///
    /// Reservar núcleos para o reconhecedor custa latência de tradução e devolve
    /// estabilidade à transcrição, que é a etapa com prazo apertado.
    /// </remarks>
    public int MaxThreads { get; init; } = 2;

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

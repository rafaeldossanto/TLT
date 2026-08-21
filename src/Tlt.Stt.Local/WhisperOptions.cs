using Tlt.Core.Models;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace Tlt.Stt.Local;

/// <summary>
/// Configuração do reconhecedor local.
/// </summary>
public sealed class WhisperOptions
{
    /// <summary>Modelo Whisper.</summary>
    /// <remarks>
    /// `Small` é o padrão por medição: RTF 13,7x numa GTX 1050 Ti, acima do alvo de 12
    /// da janela deslizante, com a mesma taxa de erro de `medium` e `turbo` pelo dobro
    /// da velocidade e um terço do tamanho.
    /// </remarks>
    public GgmlType Model { get; init; } = GgmlType.Small;

    /// <summary>Quantização dos pesos.</summary>
    public QuantizationType Quantization { get; init; } = QuantizationType.Q5_0;

    /// <summary>Idioma da fala de origem.</summary>
    /// <remarks>
    /// Fixo, e não detectado automaticamente: é mais rápido e mais confiável, e o
    /// usuário já escolheu o idioma da chamada na interface.
    /// </remarks>
    public string Language { get; init; } = "en";

    /// <summary>Caminho do modelo. Quando ausente, é baixado e guardado em cache.</summary>
    public string? ModelPath { get; init; }

    /// <summary>Diretório de cache dos modelos.</summary>
    public string CacheDirectory { get; init; } = ModelCache.DefaultDirectory;

    /// <summary>Ordem de preferência das bibliotecas de aceleração.</summary>
    /// <remarks>
    /// Vulkan primeiro por decisão de produto: roda com o driver comum, serve
    /// NVIDIA/AMD/Intel com um binário só e dispensa o CUDA Toolkit no cliente.
    /// Sem configurar isto, o Whisper.net usa CPU mesmo com as DLLs de GPU presentes.
    /// </remarks>
    public IReadOnlyList<RuntimeLibrary> RuntimeOrder { get; init; } =
        [RuntimeLibrary.Vulkan, RuntimeLibrary.Cuda12, RuntimeLibrary.Cuda, RuntimeLibrary.Cpu];

    /// <summary>Usa a GPU quando disponível.</summary>
    public bool UseGpu { get; init; } = true;
}

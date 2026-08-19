using Tlt.Core.Audio;

namespace Tlt.Core.Tests;

/// <summary>
/// Protege a regra que sustenta o desenho em camadas: o núcleo não conhece
/// tecnologia concreta. Se alguém adicionar NAudio ou WPF ao Tlt.Core para
/// "resolver rápido", o build quebra aqui em vez de a decisão se perder.
/// </summary>
public class ArquiteturaTests
{
    [Fact]
    public void Core_nao_referencia_tecnologia_concreta()
    {
        string[] proibidos = ["NAudio", "Whisper", "PresentationFramework", "WindowsBase", "LLama"];

        var referencias = typeof(IAudioSource).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var violacoes = referencias
            .Where(r => proibidos.Any(p => r.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violacoes.Length == 0,
            $"Tlt.Core passou a depender de: {string.Join(", ", violacoes)}. " +
            "O núcleo deve conter apenas abstrações — trocar o motor de STT ou portar a " +
            "interface para outra plataforma depende disso.");
    }
}

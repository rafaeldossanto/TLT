using System.IO;
using System.Text.Json;

namespace Tlt.App.Overlay;

/// <summary>
/// Preferências do overlay que sobrevivem entre sessões.
/// </summary>
public sealed class OverlaySettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 900;

    /// <summary>Esconde a legenda de capturas de tela e compartilhamento.</summary>
    public bool HideFromScreenCapture { get; set; } = true;

    /// <summary>Mostra também a hipótese, em cinza, além do texto confirmado.</summary>
    public bool ShowHypotheses { get; set; } = true;

    /// <summary>Mostra a linha com o texto no idioma original, acima da tradução.</summary>
    /// <remarks>
    /// Parte dos usuários quer só a tradução; quem entende um pouco do idioma costuma
    /// preferir ver os dois para conferir.
    /// </remarks>
    public bool ShowOriginal { get; set; } = true;

    private static string Arquivo => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TLT", "overlay.json");

    public static OverlaySettings Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<OverlaySettings>(File.ReadAllText(Arquivo)) ?? new OverlaySettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Preferência corrompida ou ilegível não pode impedir o app de abrir:
            // o custo de perder a posição da janela é muito menor que o de não subir.
        }

        return new OverlaySettings();
    }

    public void Salvar()
    {
        try
        {
            var pasta = Path.GetDirectoryName(Arquivo)!;
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // idem: falhar ao salvar preferência não derruba a aplicação
        }
    }
}

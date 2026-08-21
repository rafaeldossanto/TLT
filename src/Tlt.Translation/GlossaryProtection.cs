namespace Tlt.Translation;

/// <summary>
/// Protege termos que devem sair iguais na tradução.
/// </summary>
/// <remarks>
/// A técnica é trocar cada termo por um marcador antes de traduzir e restaurá-lo
/// depois. O marcador precisa ser algo que o modelo copie sem traduzir nem alterar:
/// verificado empiricamente com `Zx0Qv`, que atravessou intacto em todos os casos
/// testados.
///
/// Existe porque nada destrói mais rápido a confiança do usuário do que ver o nome do
/// próprio produto traduzido no meio da legenda.
/// </remarks>
public static class GlossaryProtection
{
    /// <summary>Substitui os termos do glossário por marcadores.</summary>
    public static (string Texto, IReadOnlyDictionary<string, string> Marcadores) Proteger(
        string texto,
        IReadOnlyList<string> glossario)
    {
        if (glossario.Count == 0 || string.IsNullOrEmpty(texto))
            return (texto, new Dictionary<string, string>());

        var marcadores = new Dictionary<string, string>();
        var resultado = texto;
        var indice = 0;

        // Termos mais longos primeiro: sem isso, proteger "API" antes de
        // "API gateway" quebraria o termo composto ao meio.
        foreach (var termo in glossario.OrderByDescending(t => t.Length))
        {
            if (string.IsNullOrWhiteSpace(termo)) continue;
            if (!resultado.Contains(termo, StringComparison.OrdinalIgnoreCase)) continue;

            var marcador = $"Zx{indice++}Qv";
            marcadores[marcador] = termo;
            resultado = resultado.Replace(termo, marcador, StringComparison.OrdinalIgnoreCase);
        }

        return (resultado, marcadores);
    }

    /// <summary>Devolve os termos originais ao texto traduzido.</summary>
    public static string Restaurar(string texto, IReadOnlyDictionary<string, string> marcadores)
    {
        foreach (var (marcador, termo) in marcadores)
            texto = texto.Replace(marcador, termo, StringComparison.OrdinalIgnoreCase);

        return texto;
    }
}

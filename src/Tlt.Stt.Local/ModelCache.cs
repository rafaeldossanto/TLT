namespace Tlt.Stt.Local;

/// <summary>
/// Guarda os modelos baixados em disco, para não repetir o download a cada execução.
/// </summary>
public static class ModelCache
{
    /// <summary>Diretório padrão dos modelos.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TLT", "models");

    /// <summary>
    /// Devolve o caminho do modelo, baixando se ainda não estiver em cache.
    /// </summary>
    /// <param name="directory">Onde guardar.</param>
    /// <param name="fileName">Nome do arquivo em cache.</param>
    /// <param name="download">Abre o fluxo de origem. Só é chamado se faltar o arquivo.</param>
    public static async Task<string> GetOrDownloadAsync(
        string directory,
        string fileName,
        Func<CancellationToken, Task<Stream>> download,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var destino = Path.Combine(directory, fileName);
        if (File.Exists(destino)) return destino;

        // Baixa para arquivo temporário e move só ao final. Interromper o download no
        // meio deixaria um arquivo truncado no cache, e a execução seguinte o daria
        // como válido — falha que se manifesta como modelo corrompido, bem longe da
        // causa.
        var temporario = destino + ".parcial";

        try
        {
            await using (var origem = await download(cancellationToken).ConfigureAwait(false))
            await using (var arquivo = File.Create(temporario))
            {
                await origem.CopyToAsync(arquivo, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporario, destino, overwrite: true);
            return destino;
        }
        catch
        {
            if (File.Exists(temporario)) File.Delete(temporario);
            throw;
        }
    }
}

namespace Tlt.Core.Models;

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

        for (var tentativa = 1; ; tentativa++)
        {
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
            catch (Exception e) when (tentativa < MaxTentativas && EhTransitorio(e))
            {
                // Modelos vêm de repositório público com limite de requisições, e a
                // primeira execução baixa vários arquivos seguidos — bater em 429 ou
                // numa queda de rede é rotina, não exceção. Desistir na primeira falha
                // deixaria o app inutilizável por um tropeço temporário.
                if (File.Exists(temporario)) File.Delete(temporario);
                await Task.Delay(EsperaAntesDe(tentativa), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (File.Exists(temporario)) File.Delete(temporario);
                throw;
            }
        }
    }

    private const int MaxTentativas = 4;

    private static TimeSpan EsperaAntesDe(int tentativa) => TimeSpan.FromSeconds(Math.Pow(3, tentativa));

    private static bool EhTransitorio(Exception e) => e switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => true,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.ServiceUnavailable } => true,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.BadGateway } => true,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.GatewayTimeout } => true,
        HttpRequestException { StatusCode: null } => true,   // falha de rede, sem resposta
        TaskCanceledException => true,                        // tempo esgotado
        IOException => true,
        _ => false,
    };
}

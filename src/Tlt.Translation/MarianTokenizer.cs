using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace Tlt.Translation;

/// <summary>
/// Converte texto em ids para os modelos Marian e de volta.
/// </summary>
/// <remarks>
/// São duas peças distintas, e confundi-las produz tokens errados: o SentencePiece
/// **segmenta** o texto em pedaços, e o `vocab.json` do Marian mapeia cada pedaço ao
/// id. Os ids internos do arquivo `.spm` não correspondem aos do modelo.
/// </remarks>
public sealed class MarianTokenizer
{
    /// <summary>Marcador de espaço do SentencePiece.</summary>
    private const char MarcadorEspaco = '▁';

    private readonly SentencePieceTokenizer segmentador;
    private readonly Dictionary<string, int> vocabulario;
    private readonly Dictionary<int, string> inverso;
    private readonly int idDesconhecido;

    private MarianTokenizer(SentencePieceTokenizer segmentador, Dictionary<string, int> vocabulario)
    {
        this.segmentador = segmentador;
        this.vocabulario = vocabulario;

        inverso = new Dictionary<int, string>(vocabulario.Count);
        foreach (var (token, id) in vocabulario) inverso[id] = token;

        idDesconhecido = vocabulario.GetValueOrDefault("<unk>", 0);
    }

    /// <summary>Id que encerra a sequência.</summary>
    public int IdFimDeSequencia { get; private init; }

    /// <summary>Id com que o decodificador começa.</summary>
    public int IdInicioDecodificador { get; private init; }

    /// <summary>Tamanho do vocabulário.</summary>
    public int Tamanho => vocabulario.Count;

    public static MarianTokenizer Carregar(string caminhoSpm, string caminhoVocab, int idFim, int idInicioDecodificador)
    {
        using var spm = File.OpenRead(caminhoSpm);
        var segmentador = SentencePieceTokenizer.Create(spm, false, false);

        var vocabulario = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(caminhoVocab))
                          ?? throw new InvalidDataException($"vocabulário ilegível em {caminhoVocab}");

        return new MarianTokenizer(segmentador, vocabulario)
        {
            IdFimDeSequencia = idFim,
            IdInicioDecodificador = idInicioDecodificador,
        };
    }

    /// <summary>Converte o texto em ids, já com o marcador de fim.</summary>
    public long[] Codificar(string texto)
    {
        var pedacos = segmentador.EncodeToTokens(texto, out _, considerNormalization: true);

        var ids = new long[pedacos.Count + 1];
        for (var i = 0; i < pedacos.Count; i++)
            ids[i] = vocabulario.TryGetValue(pedacos[i].Value, out var id) ? id : idDesconhecido;

        ids[^1] = IdFimDeSequencia;
        return ids;
    }

    /// <summary>Remonta o texto a partir dos ids.</summary>
    public string Decodificar(IEnumerable<long> ids)
    {
        var sb = new StringBuilder();

        foreach (var id in ids)
        {
            if (id == IdFimDeSequencia || id == IdInicioDecodificador) continue;
            if (!inverso.TryGetValue((int)id, out var token)) continue;
            sb.Append(token.Replace(MarcadorEspaco, ' '));
        }

        return sb.ToString().Trim();
    }
}

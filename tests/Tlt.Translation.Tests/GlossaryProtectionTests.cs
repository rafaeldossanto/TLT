using Tlt.Translation;

namespace Tlt.Translation.Tests;

public class GlossaryProtectionTests
{
    [Fact]
    public void Protege_e_restaura_o_termo()
    {
        var (protegido, marcadores) = GlossaryProtection.Proteger("The TLT app works", ["TLT"]);

        Assert.DoesNotContain("TLT", protegido);
        Assert.Equal("The TLT app works", GlossaryProtection.Restaurar(protegido, marcadores));
    }

    [Fact]
    public void Termo_composto_e_protegido_antes_do_termo_curto()
    {
        // Proteger "API" primeiro quebraria "API gateway" ao meio, e a restauracao
        // devolveria "API gateway" com o "gateway" ja traduzido no meio.
        var (protegido, marcadores) = GlossaryProtection.Proteger(
            "The API gateway needs the API key",
            ["API", "API gateway"]);

        var restaurado = GlossaryProtection.Restaurar(protegido, marcadores);

        Assert.Equal("The API gateway needs the API key", restaurado);
        Assert.Contains("API gateway", marcadores.Values);
    }

    [Fact]
    public void Glossario_vazio_nao_altera_o_texto()
    {
        var (protegido, marcadores) = GlossaryProtection.Proteger("nothing to protect here", []);

        Assert.Equal("nothing to protect here", protegido);
        Assert.Empty(marcadores);
    }

    [Fact]
    public void Termo_ausente_nao_gera_marcador()
    {
        // Sem isto, cada termo do glossario ocuparia um marcador mesmo sem aparecer, e
        // o texto enviado ao modelo ficaria poluido a toa.
        var (protegido, marcadores) = GlossaryProtection.Proteger("the meeting starts now", ["TLT", "Trisha"]);

        Assert.Equal("the meeting starts now", protegido);
        Assert.Empty(marcadores);
    }

    [Fact]
    public void Reconhece_o_termo_em_qualquer_caixa()
    {
        var (protegido, marcadores) = GlossaryProtection.Proteger("we shipped trisha today", ["Trisha"]);
        var restaurado = GlossaryProtection.Restaurar(protegido, marcadores);

        // A grafia do glossario prevalece: e assim que o nome do produto deve aparecer.
        Assert.Contains("Trisha", restaurado);
    }

    [Fact]
    public void Varios_termos_recebem_marcadores_distintos()
    {
        var (_, marcadores) = GlossaryProtection.Proteger("TLT talks to Trisha", ["TLT", "Trisha"]);

        Assert.Equal(2, marcadores.Count);
        Assert.Equal(marcadores.Count, marcadores.Keys.Distinct().Count());
    }

    [Fact]
    public void Termo_em_branco_no_glossario_e_ignorado()
    {
        // Um item vazio casaria com qualquer posicao do texto e destruiria a frase.
        var (protegido, _) = GlossaryProtection.Proteger("keep this intact", ["", "   "]);

        Assert.Equal("keep this intact", protegido);
    }
}

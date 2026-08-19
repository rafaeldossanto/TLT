using Tlt.Core.Transcription;

namespace Tlt.Core.Tests;

public class TranscriptSegmentTests
{
    [Fact]
    public void Duracao_e_a_diferenca_entre_inicio_e_fim()
    {
        var segmento = new TranscriptSegment(
            "hello there",
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4.5),
            "en",
            IsConfirmed: true);

        Assert.Equal(TimeSpan.FromSeconds(2.5), segmento.Duration);
    }

    [Fact]
    public void Segmento_provisorio_e_distinto_do_confirmado_com_o_mesmo_texto()
    {
        var provisorio = new TranscriptSegment("hello", TimeSpan.Zero, TimeSpan.FromSeconds(1), "en", IsConfirmed: false);
        var confirmado = provisorio with { IsConfirmed = true };

        // A janela deslizante depende dessa distincao: o mesmo texto muda de status
        // quando duas passagens concordam, e so entao vai para a traducao.
        Assert.NotEqual(provisorio, confirmado);
        Assert.Equal(provisorio.Text, confirmado.Text);
    }
}

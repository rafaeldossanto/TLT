namespace Tlt.Core.Transcription;

/// <summary>
/// Parâmetros da transcrição por janela deslizante.
/// </summary>
public sealed class SlidingWindowOptions
{
    /// <summary>Intervalo entre reprocessamentos do trecho em curso.</summary>
    /// <remarks>
    /// Medido na GTX 1050 Ti: uma passada de 10 s custa 1.083 ms, então 1,5 s deixa
    /// 72% de ocupação. Baixar para 800 ms exigiria 135% e simplesmente não fecha —
    /// a passada seguinte começaria antes de a anterior terminar.
    /// </remarks>
    public TimeSpan ReprocessInterval { get; init; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Teto de áudio acumulado antes de fechar o trecho à força.</summary>
    /// <remarks>
    /// Ao atingir este limite o texto é confirmado e o buffer recomeça, mesmo sem
    /// pausa na fala. Existe para quem fala sem parar: sem o teto, o buffer cresceria
    /// e o custo por passada subiria junto.
    ///
    /// Não descartar apenas o excedente é deliberado. Cortar o início do buffer sem
    /// fechar o trecho quebra a cadeia do LocalAgreement — a passada seguinte não tem
    /// com o que comparar e nada se confirma. Medido: com descarte parcial saíram 11
    /// confirmações em 57 s de fala; fechando o trecho, 31.
    ///
    /// Dez segundos porque o custo é quase todo fixo: encolher quase não economiza e
    /// faz o modelo perder contexto.
    /// </remarks>
    public TimeSpan WindowDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Silêncio observado no fim do buffer para dar a fala por encerrada.</summary>
    public TimeSpan EndOfSpeechMargin { get; init; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Emite hipóteses provisórias além do texto confirmado.</summary>
    /// <remarks>
    /// Ligado por padrão: é o que faz a legenda aparecer enquanto a pessoa ainda fala,
    /// em vez de surgir em bloco no fim da frase. A interface mostra o provisório em
    /// cinza para o leitor saber que aquilo ainda pode mudar.
    /// </remarks>
    public bool EmitHypotheses { get; init; } = true;
}

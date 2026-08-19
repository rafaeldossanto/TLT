using Microsoft.Extensions.Hosting;

namespace Tlt.App;

/// <summary>
/// Registro das implementações no container.
/// </summary>
/// <remarks>
/// Vindo do Spring: <c>IServiceCollection</c> é o container,
/// <c>AddSingleton/AddScoped/AddTransient</c> são o escopo dos beans,
/// <c>IOptions&lt;T&gt;</c> corresponde a <c>@ConfigurationProperties</c> e
/// <c>appsettings.json</c> a <c>application.yml</c>.
/// </remarks>
internal static class ComposicaoExtensions
{
    public static HostApplicationBuilder ConfigureTlt(this HostApplicationBuilder builder)
    {
        // As implementacoes entram aqui conforme as tasks avancam:
        //
        //   IAudioSource            -> Tlt.Audio        (task #6)
        //   IVoiceActivityDetector  -> Tlt.Audio        (task #7)
        //   ISpeechRecognizer       -> Tlt.Stt.Local    (task #8, padrao)
        //                              Tlt.Stt.Cloud    (task #8, sob escolha explicita)
        //   ITranslator             -> Tlt.Translation  (task #9, pendente da #14)
        //
        // A selecao entre local e nuvem e resolvida em runtime, mas com uma regra
        // que vem do ADR de privacidade: nuvem falhando pode cair para local
        // automaticamente; local insuficiente NAO sobe para nuvem sem o usuario
        // escolher. Ver Docs/Decisoes/Privacidade por Padrao.md
        return builder;
    }
}

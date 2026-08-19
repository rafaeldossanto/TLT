using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tlt.App;

/// <summary>
/// Composição da aplicação. É o único lugar que conhece todas as implementações
/// concretas — o resto do código depende apenas das abstrações de Tlt.Core.
/// </summary>
public partial class App : Application
{
    private IHost? host;

    /// <summary>Container de serviços, para as janelas resolverem dependências.</summary>
    public static IServiceProvider Services =>
        ((App)Current).host?.Services
        ?? throw new InvalidOperationException("O host ainda não foi construído.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        host = Host.CreateApplicationBuilder()
            .ConfigureTlt()
            .Build();

        await host.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        base.OnExit(e);
    }
}

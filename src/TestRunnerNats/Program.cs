using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAF.Hosting;
using SAF.Messaging.Nats;

namespace TestRunnerNats;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "SAF Nats Test Host";

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var loggingServices = new ServiceCollection();
                loggingServices.AddLogging(l => l.AddConfiguration(context.Configuration.GetSection("Logging")).AddConsole());
                using var loggingServiceProvider = loggingServices.BuildServiceProvider();
                var mainLogger = loggingServiceProvider.GetService<ILogger<Program>>();

                services.AddHost(context.Configuration.GetSection("ServiceHost").Bind, mainLogger);
                services.AddHostDiagnostics();
                services.AddNatsInfrastructure(context.Configuration.GetSection("Nats").Bind);
            })
            .Build();

        await host.RunAsync();
    }
}

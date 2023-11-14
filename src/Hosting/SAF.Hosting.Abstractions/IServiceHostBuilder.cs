using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting.Abstractions;

public interface IServiceHostBuilder
{
    IServiceCollection Services { get; }

    IServiceHostBuilder WithServiceHostInfo(Action<ServiceHostInfoOptions> setupAction);
}
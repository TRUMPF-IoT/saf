using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting;

public interface IServiceHostBuilder
{
    IServiceCollection Services { get; }
}

internal class ServiceHostBuilder(IServiceCollection services) : IServiceHostBuilder
{
    public IServiceCollection Services { get; } = services;
}

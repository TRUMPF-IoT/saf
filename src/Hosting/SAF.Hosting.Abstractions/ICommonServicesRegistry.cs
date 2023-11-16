using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting.Abstractions;

public interface ICommonServicesRegistry
{
    IServiceCollection Services { get; }
}
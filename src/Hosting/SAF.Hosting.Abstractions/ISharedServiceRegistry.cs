using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting.Abstractions;

public interface ISharedServiceRegistry
{
    IEnumerable<ServiceDescriptor> Services { get; }
}
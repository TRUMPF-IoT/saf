using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SAF.Hosting;

public interface IServiceAssemblySearch
{

}

internal class ServiceAssemblySearch(ILogger<ServiceAssemblySearch> logger, IOptions<ServiceAssemblySearchOptions> searchOptions) : IServiceAssemblySearch
{

}
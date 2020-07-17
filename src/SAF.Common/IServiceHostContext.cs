using Microsoft.Extensions.Configuration;

namespace SAF.Common
{
    public interface IServiceHostContext
    {
        IConfiguration Configuration { get; }
        IServiceHostEnvironment Environment { get; }
        IHostInfo HostInfo { get; }
    }
}
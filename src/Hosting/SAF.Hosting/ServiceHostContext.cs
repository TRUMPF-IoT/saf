using Microsoft.Extensions.Configuration;
using SAF.Common;

namespace SAF.Hosting
{
    internal class ServiceHostContext : IServiceHostContext
    {
        public IConfiguration Configuration { get; set; }
        public IServiceHostEnvironment Environment { get; set; }
        public IHostInfo HostInfo { get; set; }
    }
}
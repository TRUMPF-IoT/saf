using SAF.Common;

namespace SAF.Hosting
{
    internal class ServiceHostEnvironment : IServiceHostEnvironment
    {
        public string ApplicationName { get; set; }
        public string EnvironmentName { get; set; }
    }
}
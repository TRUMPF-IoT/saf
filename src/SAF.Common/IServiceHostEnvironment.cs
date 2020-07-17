namespace SAF.Common
{
    public interface IServiceHostEnvironment
    {
        string ApplicationName { get; }
        string EnvironmentName { get; }
    }
}
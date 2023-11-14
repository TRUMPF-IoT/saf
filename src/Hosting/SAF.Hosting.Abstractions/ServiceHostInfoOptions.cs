using System.Reflection;

namespace SAF.Hosting.Abstractions;

internal class ServiceHostInfoOptions
{
    public string? Id { get; set; }
    public string? ServiceHostType { get; set; }
    public string FileSystemUserBasePath { get; set; } = "tempfs";
    public string FileSystemInstallationPath { get; set; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}
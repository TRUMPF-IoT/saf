using System.Reflection;

namespace SAF.Hosting.Abstractions;

/// <summary>
/// Provides options to be used for the <see cref="IServiceHostInfo"/>.
/// </summary>
public class ServiceHostInfoOptions
{
    /// <summary>
    /// Unique-id of the service host instance. If not set, a new unique-id will be generated.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// The type of the ServiceHost (CDE, Test, ...).
    /// </summary>
    public string ServiceHostType { get; set; } = "SAF";

    /// <summary>
    /// File system base path in which application specific data is stored.
    /// </summary>
    public string FileSystemUserBasePath { get; set; } = "tempfs";

    /// <summary>
    /// File system base path representing the installation folder of the SAF host application.
    /// </summary>
    public string FileSystemInstallationPath { get; set; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}
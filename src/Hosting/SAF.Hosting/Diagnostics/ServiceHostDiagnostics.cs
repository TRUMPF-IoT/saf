// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Diagnostics;

using Microsoft.Extensions.Logging;
using Contracts;
using System.IO.Abstractions;
using Toolbox.Serialization;

internal class ServiceHostDiagnostics(ILogger<ServiceHostDiagnostics> log,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies,
        IServiceHostInfo hostInfo,
        IFileSystem fileSystem)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(CollectAndSaveDiagnostics, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void CollectAndSaveDiagnostics()
    {
        try
        {
            var nodeInfo = new SafNodeInfo(hostInfo, serviceAssemblies);

            var targetDir = fileSystem.Path.Combine(hostInfo.FileSystemUserBasePath, "diagnostics");
            if (!fileSystem.Directory.Exists(targetDir)) fileSystem.Directory.CreateDirectory(targetDir);

            var file = $"SafServiceHost_{hostInfo.Id}.json";
            var targetFile = fileSystem.Path.Combine(targetDir, file);
            if (fileSystem.File.Exists(targetFile)) fileSystem.File.Delete(targetFile);

            var serializedInfo = JsonSerializer.Serialize(nodeInfo);
            fileSystem.File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}
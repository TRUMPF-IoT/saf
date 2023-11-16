// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting.Abstractions;
using SAF.Toolbox.Serialization;

namespace SAF.Hosting.Diagnostics;

internal class ServiceHostDiagnostics(ILogger<ServiceHostDiagnostics> log,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies,
        IServiceHostInfo hostInfo)
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

            var targetDir = Path.Combine(hostInfo.FileSystemUserBasePath, "diagnostics");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var file = $"SafServiceHost_{hostInfo.Id}.json";
            var targetFile = Path.Combine(targetDir, file);
            if (File.Exists(targetFile)) File.Delete(targetFile);

            var serializedInfo = JsonSerializer.Serialize(nodeInfo);
            File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}
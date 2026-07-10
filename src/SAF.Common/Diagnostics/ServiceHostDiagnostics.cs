// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAF.PluginSystem.Hosting.Contracts;
using System.Text.Json;

internal class ServiceHostDiagnostics(
    ILogger<ServiceHostDiagnostics> log,
    IEnumerable<IPluginManifest> pluginManifests,
    IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(CollectAndSaveDiagnostics, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void CollectAndSaveDiagnostics()
    {
        try
        {
            var hostInfo = serviceProvider.GetService<IServiceHostInfo>();
            var nodeInfo = new SafNodeInfo(hostInfo, pluginManifests);

            var basePath = string.IsNullOrWhiteSpace(hostInfo?.FileSystemUserBasePath)
                ? Path.Combine(AppContext.BaseDirectory, "tempfs")
                : hostInfo!.FileSystemUserBasePath;

            var targetDir = Path.Combine(basePath, "diagnostics");
            Directory.CreateDirectory(targetDir);

            var safeHostId = string.Concat(nodeInfo.HostId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var targetFile = Path.Combine(targetDir, $"SafServiceHost_{safeHostId}.json");

            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            var serializedInfo = JsonSerializer.Serialize(nodeInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}

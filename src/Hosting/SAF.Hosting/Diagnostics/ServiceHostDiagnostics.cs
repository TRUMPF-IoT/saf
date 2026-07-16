// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;
using System.Text.Json;
using System.IO.Abstractions;

internal class ServiceHostDiagnostics(
    ILogger<ServiceHostDiagnostics> log,
    IEnumerable<IPluginAssemblyContainer> pluginAssemblyContainers,
    IServiceProvider serviceProvider,
    IFileSystem fileSystem) : IHostedService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(CollectAndSaveDiagnostics, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void CollectAndSaveDiagnostics()
    {
        try
        {
            var hostInfo = serviceProvider.GetService<IServiceHostInfo>();
            var pluginManifests = pluginAssemblyContainers.SelectMany(c => c.GetPluginManifests());
            var nodeInfo = new SafNodeInfo(hostInfo, pluginManifests);

            var basePath = string.IsNullOrWhiteSpace(hostInfo?.FileSystemUserBasePath)
                ? fileSystem.Path.Combine(AppContext.BaseDirectory, "tempfs")
                : hostInfo!.FileSystemUserBasePath;

            var targetDir = fileSystem.Path.Combine(basePath, "diagnostics");
            fileSystem.Directory.CreateDirectory(targetDir);

            var safeHostId = string.Concat(nodeInfo.HostId.Select(c => fileSystem.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var targetFile = fileSystem.Path.Combine(targetDir, $"SafServiceHost_{safeHostId}.json");

            if (fileSystem.File.Exists(targetFile))
            {
                fileSystem.File.Delete(targetFile);
            }

            var serializedInfo = JsonSerializer.Serialize(nodeInfo, _jsonOptions);
            fileSystem.File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}

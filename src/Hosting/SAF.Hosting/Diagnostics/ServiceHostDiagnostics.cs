// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Toolbox.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IHostedService = Microsoft.Extensions.Hosting.IHostedService;

namespace SAF.Hosting.Diagnostics;

internal class ServiceHostDiagnostics : IHostedService
{
    private readonly ILogger<ServiceHostDiagnostics> _log;
    private readonly IEnumerable<IServiceAssemblyManifest> _serviceAssemblies;
    private readonly IHostInfo _hostInfo;

    public ServiceHostDiagnostics(ILogger<ServiceHostDiagnostics> log,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies,
        IHostInfo hostInfo)
    {
        _log = log ?? NullLogger<ServiceHostDiagnostics>.Instance;
        _serviceAssemblies = serviceAssemblies;
        _hostInfo = hostInfo;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(CollectAndSaveDiagnostics, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void CollectAndSaveDiagnostics()
    {
        try
        {
            var nodeInfo = new SafNodeInfo(_hostInfo, _serviceAssemblies);

            var targetDir = Path.Combine(_hostInfo.FileSystemUserBasePath, "diagnostics");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var file = $"SafServiceHost_{_hostInfo.Id}.json";
            var targetFile = Path.Combine(targetDir, file);
            if (File.Exists(targetFile)) File.Delete(targetFile);

            var serializedInfo = JsonSerializer.Serialize(nodeInfo);
            File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}
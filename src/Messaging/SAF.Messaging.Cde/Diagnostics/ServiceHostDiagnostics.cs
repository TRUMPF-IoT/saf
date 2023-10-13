// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nsCDEngine.BaseClasses;
using SAF.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using IHostedService = Microsoft.Extensions.Hosting.IHostedService;

namespace SAF.Messaging.Cde.Diagnostics;

internal class ServiceHostDiagnostics : IHostedService
{
    private readonly ILogger<ServiceHostDiagnostics> _log;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostInfo _hostInfo;

    public ServiceHostDiagnostics(ILogger<ServiceHostDiagnostics> log, IServiceProvider serviceProvider, IHostInfo hostInfo)
    {
        _log = log ?? NullLogger<ServiceHostDiagnostics>.Instance;
        _serviceProvider = serviceProvider;
        _hostInfo = hostInfo;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(CollectAndSaveDiagnostics, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void CollectAndSaveDiagnostics()
    {
        try
        {
            _ = _serviceProvider.GetRequiredService<CdeApplication>();

            var nodeInfo = new CdeNodeInfo(_hostInfo);

            var targetDir = Path.Combine(_hostInfo.FileSystemUserBasePath, "diagnostics");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var file = $"CdeServiceHost_{_hostInfo.Id}.json";
            var targetFile = Path.Combine(targetDir, file);
            if (File.Exists(targetFile)) File.Delete(targetFile);

            var serializedInfo = TheCommonUtils.SerializeObjectToJSONString(nodeInfo);
            File.WriteAllText(targetFile, serializedInfo);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to collect and save service host diagnostic information!");
        }
    }
}
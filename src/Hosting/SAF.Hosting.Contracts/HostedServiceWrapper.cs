// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

using System.Threading;
using System.Threading.Tasks;

public class HostedServiceWrapper<TService>(TService hostedService) : IHostedServiceAsync where TService : class, IHostedService
{
    public Task StartAsync(CancellationToken cancelToken)
        => Task.Run(hostedService.Start, cancelToken);

    public Task StopAsync(CancellationToken cancelToken)
        => Task.Run(hostedService.Stop, cancelToken);
}
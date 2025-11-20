// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.TestServices
{
    using Contracts;
    using System.Threading;
    using System.Threading.Tasks;

    // DummyService used for hosting tests only
    public class DummyService : IHostedServiceAsync
    {
        public Task StartAsync(CancellationToken cancelToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancelToken) => Task.CompletedTask;
    }
}
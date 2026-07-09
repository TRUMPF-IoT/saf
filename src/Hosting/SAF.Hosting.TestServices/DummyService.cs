// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
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
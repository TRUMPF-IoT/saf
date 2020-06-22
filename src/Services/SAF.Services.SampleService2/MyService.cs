// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using SAF.Common;

namespace SAF.Services.SampleService2
{
    public class MyService : IHostedService
    {
        private readonly ILogger<MyService> _log;

        public MyService(ILogger<MyService> log)
        {
            _log = log;
        }

        public void Start()
        {
            _log.LogInformation("START");
        }

        public void Stop()
        {
            _log.LogInformation("STOP");
        }

        public void Kill()
        {
            _log.LogInformation("ADIOS");
        }
    }
}
// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.TestServices
{
    using Abstractions;

    public class DummyService : IHostedService
    {
        public void Start()
        {
            // DummyService used for hosting tests only
        }

        public void Stop()
        {
            // DummyService used for hosting tests only
        }

        public void Kill()
        {
            // DummyService used for hosting tests only
        }
    }
}
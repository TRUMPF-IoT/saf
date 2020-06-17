// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using SAF.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAF.Hosting.Diagnostics
{
    internal class SafNodeInfo
    {
        private readonly IHostInfo _hostInfo;

        public SafNodeInfo(IHostInfo hostInfo, IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
        {
            _hostInfo = hostInfo;
            SafServices = ReadServiceInfos(serviceAssemblies);
        }

        public string HostId => _hostInfo.Id;
        public SafVersionInfo SafVersionInfo { get; } = new SafVersionInfo();
        public IEnumerable<SafServiceInfo> SafServices { get; }
        public DateTimeOffset UpSince => _hostInfo.UpSince;

        private IEnumerable<SafServiceInfo> ReadServiceInfos(IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
        {
            return serviceAssemblies
                .Select(a => { try { return new SafServiceInfo(a); } catch { return null; } })
                .Where(si => si != null);
        }
    }
}

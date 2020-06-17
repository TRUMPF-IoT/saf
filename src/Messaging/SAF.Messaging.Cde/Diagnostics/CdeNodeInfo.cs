// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using SAF.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SAF.Messaging.Cde.Diagnostics
{
    internal class CdeNodeInfo
    {
        private IHostInfo _hostInfo;

        public CdeNodeInfo(IHostInfo hostInfo)
        {
            _hostInfo = hostInfo;
        }

        public string SafHostId => _hostInfo.Id;
        public CdeServiceHostInfo ServiceHostInfo { get; } = new CdeServiceHostInfo();
        public CdeVersionInfo CdeVersionInfo { get; } = new CdeVersionInfo();

        public IEnumerable<CdePluginInfo> CdePlugIns { get; } = ReadPluginInfos();

        private static IEnumerable<CdePluginInfo> ReadPluginInfos()
        {
            return TheBaseAssets.MyCDEPluginTypes
                .Select(pt => CreatePluginInfo(pt.Key, pt.Value))
                .Where(pi => pi != null);
        }

        private static CdePluginInfo CreatePluginInfo(string plugInKey, Type plugInType)
        {
            if(string.IsNullOrEmpty(plugInType.Assembly.Location)) return null;
            var fvi = FileVersionInfo.GetVersionInfo(plugInType.Assembly.Location);
            var info = new CdePluginInfo
            {
                Name = plugInKey,
                Version = fvi.FileVersion,
                BuildNumber = plugInType.Assembly.GetName().Version.ToString(),
                BuildDate = System.IO.File.GetLastWriteTimeUtc(plugInType.Assembly.Location)
            };
            return info;
        }
    }
}

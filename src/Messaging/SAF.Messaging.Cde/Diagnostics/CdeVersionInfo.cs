// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Diagnostics;
using System.IO;
using nsCDEngine.BaseClasses;

namespace SAF.Messaging.Cde.Diagnostics
{
    public class CdeVersionInfo
    {
        public CdeVersionInfo()
        {
            var cdeTpe = typeof(TheBaseAssets);
            var assembly = cdeTpe.Assembly;
            BuildNumber = assembly.GetName().Version.ToString();
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                Version = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
                BuildDate = File.GetLastWriteTimeUtc(assembly.Location);
            }
        }

        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public DateTimeOffset BuildDate { get; set; }
    }
}

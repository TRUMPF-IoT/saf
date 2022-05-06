// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Diagnostics;
using System.IO;

namespace SAF.Hosting.Diagnostics
{
    internal class SafVersionInfo
    {
        public SafVersionInfo()
        {
            var safType = typeof(SafVersionInfo);
            var assembly = safType.Assembly;
            BuildNumber = assembly.GetName().Version.ToString();
            if(!string.IsNullOrEmpty(assembly.Location))
            {
                Version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
                BuildDate = File.GetLastWriteTimeUtc(assembly.Location);
            }
        }

        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public DateTimeOffset BuildDate { get; set; }
    }
}
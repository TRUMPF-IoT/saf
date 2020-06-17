// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using SAF.Common;
using System;
using System.Diagnostics;

namespace SAF.Hosting.Diagnostics
{
    public class SafServiceInfo
    {
        public SafServiceInfo(IServiceAssemblyManifest assembly)
        {
            ReadServiceInfo(assembly);
        }

        public string Name { get; set; }
        public string FriendlyName { get; set; }
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public DateTimeOffset BuildDate { get; set; }

        private void ReadServiceInfo(IServiceAssemblyManifest assembly)
        {
            var type = assembly.GetType();

            var fvi = FileVersionInfo.GetVersionInfo(type.Assembly.Location);

            Name = type.AssemblyQualifiedName;
            FriendlyName = assembly.FriendlyName;
            Version = fvi.FileVersion;
            BuildNumber = type.Assembly.GetName().Version.ToString();
            BuildDate = System.IO.File.GetLastWriteTimeUtc(type.Assembly.Location);
        }
    }
}
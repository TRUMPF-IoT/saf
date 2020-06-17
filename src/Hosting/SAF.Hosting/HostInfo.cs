// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using SAF.Common;
using System;

namespace SAF.Hosting
{
    public class HostInfo : IHostInfo
    {
        internal HostInfo()
        { }

        public string Id { get; set; }

        public string ServiceHostType { get; set; }

        public string FileSystemUserBasePath { get; set; }

        public string FileSystemInstallationPath { get; set; }

        public DateTimeOffset UpSince { get; } = DateTimeOffset.Now;
    }
}

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

namespace SAF.Messaging.Cde.Diagnostics
{
    public class CdePluginInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public DateTimeOffset BuildDate { get; set; }
    }
}
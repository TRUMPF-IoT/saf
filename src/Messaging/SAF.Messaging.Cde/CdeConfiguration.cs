// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System.Collections.Generic;

namespace SAF.Messaging.Cde
{
    public class CdeConfiguration
    {
        public bool UseRandomScopeId { get; set; }
        public string ScopeId { get; set; }
        public string ApplicationId { get; set; }
        public string StorageId { get; set; }
        public string ApplicationName { get; set; }
        public string ApplicationTitle { get; set; }
        public double ApplicationVersion { get; set; }
        public string PortalTitle { get; set; }
        public string DebugLevel { get; set; }
        public ushort HttpPort { get; set; }
        public ushort WsPort { get; set; }
        public string CloudServiceRoutes { get; set; }
        public string LocalServiceRoutes { get; set; }
        public bool IsCloudService { get; set; }
        public bool AllowLocalHost { get; set; }
        public bool UseRandomDeviceId { get; set; }
        public bool UseUserMapper { get; set; }
        public bool FailOnAdminCheck { get; set; }
        public bool DontVerifyTrust { get; set; }
        public string LogIgnore { get; set; }
        public int PreShutdownDelay { get; set; }

        public IDictionary<string, string> AdditionalArguments { get; set; } = new Dictionary<string, string>();
    }
}

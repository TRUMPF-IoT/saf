// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde;

public class CdeCryptoLibConfig
{
    public string DllName { get; set; } = default!;
    public string AppCertAssemblyPath { get; set; } = default!;
    public bool DontVerifyTrust { get; set; }
    public bool DontVerifyIntegrity { get; set; }
    public bool VerifyTrustPath { get; set; } = true;
}

public class CdeConfiguration
{
    public string? ScopeId { get; set; }
    public string ApplicationId { get; set; } = default!;
    public string? StorageId { get; set; }
    public string? ApplicationName { get; set; }
    public string? ApplicationTitle { get; set; }
    public double ApplicationVersion { get; set; }
    public string? PortalTitle { get; set; }
    public string DebugLevel { get; set; } = "Warning";
    public ushort HttpPort { get; set; }
    public ushort WsPort { get; set; }
    public string? CloudServiceRoutes { get; set; }
    public string? LocalServiceRoutes { get; set; }
    public bool IsCloudService { get; set; }
    public bool AllowLocalHost { get; set; }
    public bool UseRandomDeviceId { get; set; }
    public bool UseUserMapper { get; set; }
    public bool FailOnAdminCheck { get; set; }
    public bool DontVerifyTrust { get; set; }
    public string? LogIgnore { get; set; }
    public int PreShutdownDelay { get; set; }

    public CdeCryptoLibConfig? CryptoLibConfig { get; set; }

    public IDictionary<string, string> AdditionalArguments { get; set; } = new Dictionary<string, string>();
}
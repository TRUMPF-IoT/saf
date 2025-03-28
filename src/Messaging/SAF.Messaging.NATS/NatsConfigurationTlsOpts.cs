﻿// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats;

public class NatsConfigurationTlsOpts
{
    public static readonly NatsConfigurationTlsOpts Default = new();
    public string? CertFile { get; set; }
    public string? KeyFile { get; set; }
    public string? KeyFilePassword { get; set; }
    public string? CertBundleFile { get; set; }
    public string? CertBundleFilePassword { get; set; }
    public string? CaFile { get; set; }
    public bool InsecureSkipVerify { get; set; }
    public NatsTlsMode Mode { get; set; }
}

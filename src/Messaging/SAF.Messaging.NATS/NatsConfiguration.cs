// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats;

public class NatsConfiguration
{
    public string Url { get; set; } = "";
    public bool Verbose { get; set; }
    public NatsConfigurationAuthOpts AuthOpts { get; set; } = NatsConfigurationAuthOpts.Default;
    public NatsConfigurationTlsOpts TlsOpts { get; set; } = NatsConfigurationTlsOpts.Default;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxReconnectRetry { get; set; } = -1;
    public string? ProxyUrl { get; set; }
    public string? ProxyUser { get; set; }
    public string? ProxyPassword { get; set; }
}

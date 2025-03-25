// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using NATS.Client.Core;
using SAF.Common;

namespace SAF.Messaging.Nats;

internal class NatsMessagingConfiguration
{
    private const int DefaultTimeout = 30;
    private const int DefaultMaxReconnectRetry = -1;
    private readonly IDictionary<string, string> _config;

    public NatsMessagingConfiguration()
        : this(new Dictionary<string, string>())
    { }

    public NatsMessagingConfiguration(MessagingConfiguration config)
        : this(config.Config ?? new Dictionary<string, string>())
    { }

    public NatsMessagingConfiguration(IDictionary<string, string> config)
    {
        _config = config;
    }

    public string? Url => _config.TryGetValue("Url", out var url) ? url : null;

    public bool Verbose => _config.TryGetValue("Verbose", out var verbose) && bool.Parse(verbose);

    public int ConnectionTimeout => _config.TryGetValue("ConnectionTimeoutInSeconds", out var connectionTimeout) ? int.Parse(connectionTimeout) : DefaultTimeout;

    public int RequestTimeout => _config.TryGetValue("RequestTimeoutInSeconds", out var requestTimeout) ? int.Parse(requestTimeout) : DefaultTimeout;

    public int CommandTimeout => _config.TryGetValue("CommandTimeoutInSeconds", out var commandTimeout) ? int.Parse(commandTimeout) : DefaultTimeout;

    public int MaxReconnectRetry => _config.TryGetValue("MaxReconnectRetry", out var maxReconnectRetry) ? int.Parse(maxReconnectRetry) : DefaultMaxReconnectRetry;

    public string? ProxyUrl => _config.TryGetValue("ProxyUrl", out var proxyUrl) ? proxyUrl : null;

    public string? ProxyUser => _config.TryGetValue("ProxyUser", out var proxyUser) ? proxyUser : null;

    public string? ProxyPassword => _config.TryGetValue("ProxyPassword", out var proxyPassword) ? proxyPassword : null;

    public string? Username => _config.TryGetValue("AuthOpts_Username", out var username) ? username : null;

    public string? Password => _config.TryGetValue("AuthOpts_Password", out var password) ? password : null;

    public string? Token => _config.TryGetValue("AuthOpts_Token", out var token) ? token : null;

    public string? Jwt => _config.TryGetValue("AuthOpts_Jwt", out var jwt) ? jwt : null;

    public string? NKey => _config.TryGetValue("AuthOpts_Nkey", out var nkey) ? nkey : null;

    public string? Seed => _config.TryGetValue("AuthOpts_Seed", out var seed) ? seed : null;

    public string? CredsFile => _config.TryGetValue("AuthOpts_CredsFile", out var seed) ? seed : null;

    public string? NKeyFile => _config.TryGetValue("AuthOpts_NkeyFile", out var nkeyFile) ? nkeyFile : null;

    public string? CertFile => _config.TryGetValue("TlsOpts_CertFile", out var certFile) ? certFile : null;

    public string? KeyFile => _config.TryGetValue("TlsOpts_KeyFile", out var keyFile) ? keyFile : null;

    public string? KeyFilePassword => _config.TryGetValue("TlsOpts_KeyFilePassword", out var keyFilePassword) ? keyFilePassword : null;

    public string? CertBundleFile => _config.TryGetValue("TlsOpts_CertBundleFile", out var certBundleFile) ? certBundleFile : null;

    public string? CertBundleFilePassword => _config.TryGetValue("TlsOpts_CertBundleFilePassword", out var certBundleFilePassword) ? certBundleFilePassword : null;

    public string? CaFile => _config.TryGetValue("TlsOpts_CaFile", out var caFile) ? caFile : null;

    public bool InsecureSkipVerify => _config.TryGetValue("TlsOpts_InsecureSkipVerify", out var insecureSkipVerify) && bool.Parse(insecureSkipVerify);

    public TlsMode Mode => _config.TryGetValue("TlsOpts_Mode", out var mode) ? Enum.Parse<TlsMode>(mode) : TlsMode.Auto;
}

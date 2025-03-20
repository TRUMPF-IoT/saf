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

    public string? Url => _config.TryGetValue("url", out var url) ? url : null;

    public bool Verbose => _config.TryGetValue("verbose", out var verbose) && bool.Parse(verbose);

    public int ConnectionTimeout => _config.TryGetValue("connectionTimeoutInSeconds", out var connectionTimeout) ? int.Parse(connectionTimeout) : DefaultTimeout;

    public int RequestTimeout => _config.TryGetValue("requestTimeoutInSeconds", out var requestTimeout) ? int.Parse(requestTimeout) : DefaultTimeout;

    public int CommandTimeout => _config.TryGetValue("commandTimeoutInSeconds", out var commandTimeout) ? int.Parse(commandTimeout) : DefaultTimeout;

    public int MaxReconnectRetry => _config.TryGetValue("maxReconnectRetry", out var maxReconnectRetry) ? int.Parse(maxReconnectRetry) : DefaultMaxReconnectRetry;

    public string? ProxyUrl => _config.TryGetValue("proxyUrl", out var proxyUrl) ? proxyUrl : null;

    public string? ProxyUser => _config.TryGetValue("proxyUser", out var proxyUser) ? proxyUser : null;

    public string? ProxyPassword => _config.TryGetValue("proxyPassword", out var proxyPassword) ? proxyPassword : null;

    public string? Username => _config.TryGetValue("authOpts:username", out var username) ? username : null;

    public string? Password => _config.TryGetValue("authOpts:password", out var password) ? password : null;

    public string? Token => _config.TryGetValue("authOpts:token", out var token) ? token : null;

    public string? Jwt => _config.TryGetValue("authOpts:jwt", out var jwt) ? jwt : null;

    public string? NKey => _config.TryGetValue("authOpts:nkey", out var nkey) ? nkey : null;

    public string? Seed => _config.TryGetValue("authOpts:seed", out var seed) ? seed : null;

    public string? CredsFile => _config.TryGetValue("authOpts:credsFile", out var seed) ? seed : null;

    public string? NKeyFile => _config.TryGetValue("authOpts:nkeyFile", out var nkeyFile) ? nkeyFile : null;

    public string? CertFile => _config.TryGetValue("tlsOpts:certFile", out var certFile) ? certFile : null;

    public string? KeyFile => _config.TryGetValue("tlsOpts:keyFile", out var keyFile) ? keyFile : null;

    public string? KeyFilePassword => _config.TryGetValue("tlsOpts:keyFilePassword", out var keyFilePassword) ? keyFilePassword : null;

    public string? CertBundleFile => _config.TryGetValue("tlsOpts:certBundleFile", out var certBundleFile) ? certBundleFile : null;

    public string? CertBundleFilePassword => _config.TryGetValue("tlsOpts:certBundleFilePassword", out var certBundleFilePassword) ? certBundleFilePassword : null;

    public string? CaFile => _config.TryGetValue("tlsOpts:caFile", out var caFile) ? caFile : null;

    public bool InsecureSkipVerify => _config.TryGetValue("tlsOpts:insecureSkipVerify", out var insecureSkipVerify) && bool.Parse(insecureSkipVerify);

    public TlsMode Mode => _config.TryGetValue("tlsOpts:mode", out var mode) ? Enum.Parse<TlsMode>(mode) : TlsMode.Auto;
}

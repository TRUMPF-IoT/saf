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

    public string? Username => _config.TryGetValue("authOpt:username", out var username) ? username : null;

    public string? Password => _config.TryGetValue("authOpt:password", out var password) ? password : null;

    public string? Token => _config.TryGetValue("authOpt:token", out var token) ? token : null;

    public string? Jwt => _config.TryGetValue("authOpt:jwt", out var jwt) ? jwt : null;

    public string? NKey => _config.TryGetValue("authOpt:nkey", out var nkey) ? nkey : null;

    public string? Seed => _config.TryGetValue("authOpt:seed", out var seed) ? seed : null;

    public string? CredsFile => _config.TryGetValue("authOpt:credsFile", out var seed) ? seed : null;

    public string? NKeyFile => _config.TryGetValue("authOpt:nkeyFile", out var nkeyFile) ? nkeyFile : null;
}

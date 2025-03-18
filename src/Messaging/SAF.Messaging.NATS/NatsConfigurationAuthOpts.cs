namespace SAF.Messaging.Nats;

public class NatsConfigurationAuthOpts
{
    public static readonly NatsConfigurationAuthOpts Default = new();

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public string? Jwt { get; set; }
    public string? NKey { get; set; }
    public string? Seed { get; set; }
    public string? CredsFile { get; set; }
    public string? NKeyFile { get; set; }
}

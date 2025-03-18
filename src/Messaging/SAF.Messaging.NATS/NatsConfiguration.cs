namespace SAF.Messaging.Nats;

public class NatsConfiguration
{
    public string Url { get; set; } = "";
    public bool Verbose { get; set; }
    public NatsConfigurationAuthOpts AuthOpts { get; set; } = NatsConfigurationAuthOpts.Default;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxReconnectRetry { get; set; } = -1;
}

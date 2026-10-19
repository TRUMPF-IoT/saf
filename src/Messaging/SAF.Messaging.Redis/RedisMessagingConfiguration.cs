// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Redis;

using SAF.Messaging.Contracts;

internal class RedisMessagingConfiguration
{
    private readonly IDictionary<string, string> _config;

    public RedisMessagingConfiguration()
        : this(new Dictionary<string, string>())
    { }
    public RedisMessagingConfiguration(MessagingConfiguration config)
        : this(config.Config ?? new Dictionary<string, string>())
    { }

    public RedisMessagingConfiguration(IDictionary<string, string> config)
    {
        // A key copied from another transport's route must not silently miss because of its casing.
        _config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in config) _config[key] = value;
    }

    public string? ConnectionString
        => _config.TryGetValue("connectionString", out var connString) ? connString : null;

    public bool? EnableBinaryPayloads
        => _config.TryGetValue("enableBinaryPayloads", out var enable) ? bool.Parse(enable) : null;
}



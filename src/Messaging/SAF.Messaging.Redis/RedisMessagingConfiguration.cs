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
        _config = config;
    }

    public string? ConnectionString
        => _config.TryGetValue("connectionString", out var connString) ? connString : null;
}



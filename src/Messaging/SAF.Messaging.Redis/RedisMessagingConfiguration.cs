// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System.Collections.Generic;
using SAF.Common;

namespace SAF.Messaging.Redis
{
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
}
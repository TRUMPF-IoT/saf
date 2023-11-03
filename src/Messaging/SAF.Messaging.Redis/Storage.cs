// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Linq;
using StackExchange.Redis;
using SAF.Common;

namespace SAF.Messaging.Redis
{
    public class Storage : IStorageInfrastructure, IDisposable
    {
        private const string GlobalStorageArea = "global";
        private readonly IConnectionMultiplexer _connection;
        private readonly IDatabase _database;
        private readonly IServer _server;

        public Storage(IConnectionMultiplexer connection, ConfigurationOptions options)
        {
            _connection = connection;

            _database = connection.GetDatabase();
            _server = connection.GetServer(options.EndPoints.First());
        }

        public IStorageInfrastructure Set(string key, string value)
        {
            return Set(GlobalStorageArea, key, value);
        }

        public IStorageInfrastructure Set(string area, string key, string value)
        {
            if (!_database.StringSet(BuildRedisDbKey(area, key), value)) throw new Exception($"Redis-Storage: Failed to set string value at {area}:{key}");
            return this;
        }

        public IStorageInfrastructure Set(string key, byte[] value)
        {
            return Set(GlobalStorageArea, key);
        }

        public IStorageInfrastructure Set(string area, string key, byte[] value)
        {
            if (!_database.StringSet(BuildRedisDbKey(area, key), value)) throw new Exception($"Redis-Storage: Failed to set binary value at {key}");
            return this;
        }

        public string? GetString(string key)
        {
            return GetString(GlobalStorageArea, key);
        }

        public string? GetString(string area, string key)
        {
            return _database.StringGet(BuildRedisDbKey(area, key));
        }

        public byte[]? GetBytes(string key)
        {
            return GetBytes(GlobalStorageArea, key);
        }

        public byte[]? GetBytes(string area, string key)
        {
            return _database.StringGet(BuildRedisDbKey(area, key));
        }

        public IStorageInfrastructure RemoveKey(string key)
            => RemoveKey(GlobalStorageArea, key);

        public IStorageInfrastructure RemoveKey(string area, string key)
        {
            _database.KeyDelete(BuildRedisDbKey(area, key));
            return this;
        }

        public IStorageInfrastructure RemoveArea(string area)
        {
            if (area.ToLowerInvariant() == GlobalStorageArea)
                throw new NotSupportedException("It is not allowed to delete the global storage area.");

            var keys = _server.Keys(-1, $"{area.ToLowerInvariant()}:*");
            _database.KeyDelete(keys.ToArray());

            return this;
        }

        private static string BuildRedisDbKey(string area, string key)
            => $"{area.ToLowerInvariant()}:{key.ToLowerInvariant()}";

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
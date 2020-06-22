// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using StackExchange.Redis;
using SAF.Common;

namespace SAF.Messaging.Redis
{
    public class Storage : IStorageInfrastructure, IDisposable
    {
        private const string GobalStorageArea = "global";
        private readonly IConnectionMultiplexer _connection;
        private readonly IDatabase _database;

        public Storage(IConnectionMultiplexer connection)
        {
            _connection = connection;
            _database = connection.GetDatabase();
        }

        public IStorageInfrastructure Set(string key, string value)
        {
            return Set(GobalStorageArea, key, value);
        }

        public IStorageInfrastructure Set(string area, string key, string value)
        {
            if (!_database.StringSet(BuildRedisDbKey(area, key), value)) throw new Exception($"Redis-Storage: Failed to set string value at {area}:{key}");
            return this;
        }

        public IStorageInfrastructure Set(string key, byte[] value)
        {
            return Set(GobalStorageArea, key);
        }

        public IStorageInfrastructure Set(string area, string key, byte[] value)
        {
            if (!_database.StringSet(BuildRedisDbKey(area, key), value)) throw new Exception($"Redis-Storage: Failed to set binary value at {key}");
            return this;
        }

        public string GetString(string key)
        {
            return GetString(GobalStorageArea, key);
        }

        public string GetString(string area, string key)
        {
            return _database.StringGet(BuildRedisDbKey(area, key));
        }

        public byte[] GetBytes(string key)
        {
            return GetBytes(GobalStorageArea, key);
        }

        public byte[] GetBytes(string area, string key)
        {
            return _database.StringGet(BuildRedisDbKey(area, key));
        }

        private static string BuildRedisDbKey(string area, string key) => $"{area.ToLowerInvariant()}:{key.ToLowerInvariant()}";

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
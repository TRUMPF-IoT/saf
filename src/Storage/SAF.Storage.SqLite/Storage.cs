// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using System;
using System.Data.SQLite;
using System.Threading;

namespace SAF.Storage.SqLite
{
    public class Storage : IStorageInfrastructure, IDisposable
    {
        private readonly SQLiteConnection _connection;
        private readonly ReaderWriterLockSlim _syncDbAccess = new(LockRecursionPolicy.SupportsRecursion);

        private const string GlobalStorageArea = "global";
        private const string BytesValue = "bytesValue";
        private const string StringValue = "stringValue";

        public Storage(SQLiteConnection connection)
        {
            _connection = connection;
            _connection.Open();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool cleanUpManagedAndNative)
        {
            _connection.Close();

            if (cleanUpManagedAndNative)
            {
                _connection?.Dispose();
            }
        }

        public byte[] GetBytes(string key)
        {
            return GetValue(GlobalStorageArea, key, BytesValue) as byte[];
        }


        public byte[] GetBytes(string area, string key)
        {
            return GetValue(area, key, BytesValue) as byte[];
        }

        public IStorageInfrastructure RemoveKey(string key)
            => RemoveKey(GlobalStorageArea, key);

        public IStorageInfrastructure RemoveKey(string area, string key)
        {
            _syncDbAccess.EnterUpgradeableReadLock();
            try
            {
                area = CorrectLegacyAreas(area);
                if (!DoesTableExist(area)) return this;

                _syncDbAccess.EnterWriteLock();
                try
                {
                    using var command = new SQLiteCommand($"DELETE FROM {area} WHERE id=@id", _connection);
                    command.Parameters.AddWithValue("@id", key);
                    command.ExecuteNonQuery();
                }
                finally
                {
                    _syncDbAccess.ExitWriteLock();
                }
            }
            finally
            {
                _syncDbAccess.ExitUpgradeableReadLock();
            }

            return this;
        }

        public IStorageInfrastructure RemoveArea(string area)
        {
            area = CorrectLegacyAreas(area);
            if (area == GlobalStorageArea)
                throw new NotSupportedException("It is not allowed to delete the global storage area.");

            _syncDbAccess.EnterWriteLock();
            try
            {
                using var command = new SQLiteCommand($"DROP TABLE IF EXISTS {area}", _connection);
                command.ExecuteNonQuery();
            }
            finally
            {
                _syncDbAccess.ExitWriteLock();
            }

            return this;
        }

        public string GetString(string key)
        {
            return GetValue(GlobalStorageArea, key, StringValue) as string;
        }

        public string GetString(string area, string key)
        {
            return GetValue(area, key, StringValue) as string;
        }

        private object GetValue(string area, string key, string valueType)
        {
            _syncDbAccess.EnterReadLock();
            try
            {
                area = CorrectLegacyAreas(area);
                if (!DoesTableExist(area)) return null;

                using var command = new SQLiteCommand($"SELECT {valueType} FROM {area} WHERE id=@id", _connection);
                command.Parameters.AddWithValue("@id", key);
                return command.ExecuteScalar();
            }
            finally
            {
                _syncDbAccess.ExitReadLock();
            }
        }

        private bool DoesTableExist(string area)
        {
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName", _connection);
            command.Parameters.AddWithValue("@tableName", area);
            return !string.IsNullOrWhiteSpace(command.ExecuteScalar()?.ToString());
        }

        public IStorageInfrastructure Set(string key, string value)
        {
            SaveValue(GlobalStorageArea, key, value, StringValue);
            return this;
        }

        public IStorageInfrastructure Set(string area, string key, string value)
        {
            SaveValue(area, key, value, StringValue);
            return this;
        }

        public IStorageInfrastructure Set(string key, byte[] value)
        {
            SaveValue(GlobalStorageArea, key, value, BytesValue);
            return this;
        }

        public IStorageInfrastructure Set(string area, string key, byte[] value)
        {
            SaveValue(area, key, value, BytesValue);
            return this;
        }

        private void SaveValue(string area, string key, object value, string rowType)
        {
            _syncDbAccess.EnterWriteLock();
            try
            {
                area = CorrectLegacyAreas(area);
                CreateTable(area);

                using var cmd = new SQLiteCommand($"REPLACE INTO {area}(id, {rowType}) VALUES(@id, @value)", _connection);
                cmd.Parameters.AddWithValue("@id", key);
                cmd.Parameters.AddWithValue("@value", value);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                _syncDbAccess.ExitWriteLock();
            }
        }

        private void CreateTable(string tableName)
        {
            using var cmd = new SQLiteCommand(_connection);
            cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName}(id string PRIMARY KEY, {StringValue} TEXT, {BytesValue} BLOB)";
            cmd.ExecuteNonQuery();
        }

        private string CorrectLegacyAreas(string area)
            => area.Replace(".", "_").Replace("/", "_");
    }
}

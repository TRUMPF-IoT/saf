// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using System;
using System.Data.SQLite;

namespace SAF.Storage.SqLite
{
    public class Storage : IStorageInfrastructure, IDisposable
    {
        readonly SQLiteConnection _connection;
        readonly string bytesValue = "bytesValue";
        readonly string stringValue = "stringValue";

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
            return GetValue("globals", key, bytesValue) as byte[];
        }


        public byte[] GetBytes(string area, string key)
        {
            return GetValue(area, key, bytesValue) as byte[];
        }

        public string GetString(string key)
        {
            return GetValue("globals", key, stringValue) as string;
        }

        public string GetString(string area, string key)
        {
            return GetValue(area, key, stringValue) as string;
        }

        private object GetValue(string area, string key, string valueType)
        {
            area = CorrectLegacyAreas(area);
            if (!DoesTableExist(area)) return null;

            using var command = new SQLiteCommand($"SELECT {valueType} FROM {area} WHERE id=@id", _connection);
            command.Parameters.AddWithValue("@id", key);
            return command.ExecuteScalar();
        }

        private bool DoesTableExist(string area)
        {
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName", _connection);
            command.Parameters.AddWithValue("@tableName", area);
            return !string.IsNullOrWhiteSpace(command.ExecuteScalar()?.ToString());
        }

        public IStorageInfrastructure Set(string key, string value)
        {
            SaveValue("globals", key, value, stringValue);
            return this;
        }

        public IStorageInfrastructure Set(string area, string key, string value)
        {
            SaveValue(area, key, value, stringValue);
            return this;
        }

        public IStorageInfrastructure Set(string key, byte[] value)
        {
            SaveValue("globals", key, value, bytesValue);
            return this;
        }

        public IStorageInfrastructure Set(string area, string key, byte[] value)
        {
            SaveValue(area, key, value, bytesValue);
            return this;
        }

        private void SaveValue(string area, string key, object value, string rowType)
        {
            area = CorrectLegacyAreas(area);
            CreateTable(area);

            using var cmd = new SQLiteCommand($"REPLACE INTO {area}(id, {rowType}) VALUES(@id, @value)", _connection);
            cmd.Parameters.AddWithValue("@id", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }

        private void CreateTable(string tableName)
        {
            using var cmd = new SQLiteCommand(_connection);
            cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName}(id string PRIMARY KEY, {stringValue} TEXT, {bytesValue} BLOB)";
            cmd.ExecuteNonQuery();
        }

        private string CorrectLegacyAreas(string area)
        {
            return area.Replace(".", "_").Replace("/", "_");
        }
    }
}

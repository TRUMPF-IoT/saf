// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite;
using SAF.Common;
using Microsoft.Data.Sqlite;

public class Storage : IStorageInfrastructure, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _syncDbAccess = new();

    private const string GlobalStorageArea = "global";
    private const string BytesValue = "bytesValue";
    private const string StringValue = "stringValue";

    public Storage(SqliteConnection connection)
    {
        _connection = connection;
        _connection.Open();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(!disposing) return;

        lock (_syncDbAccess)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }

    public byte[]? GetBytes(string key)
    {
        return GetValue(GlobalStorageArea, key, BytesValue) as byte[];
    }


    public byte[]? GetBytes(string area, string key)
    {
        return GetValue(area, key, BytesValue) as byte[];
    }

    public IStorageInfrastructure RemoveKey(string key)
        => RemoveKey(GlobalStorageArea, key);

    public IStorageInfrastructure RemoveKey(string area, string key)
    {
        lock (_syncDbAccess)
        {
            area = CorrectLegacyAreas(area);
            if (!DoesTableExist(area)) return this;

            using var command = new SqliteCommand($"DELETE FROM {area} WHERE id=@id", _connection);
            command.Parameters.AddWithValue("@id", key);
            command.ExecuteNonQuery();
        }

        return this;
    }

    public IStorageInfrastructure RemoveArea(string area)
    {
        area = CorrectLegacyAreas(area);
        if (area == GlobalStorageArea)
            throw new NotSupportedException("It is not allowed to delete the global storage area.");

        lock (_syncDbAccess)
        {
            using var command = new SqliteCommand($"DROP TABLE IF EXISTS {area}", _connection);
            command.ExecuteNonQuery();
        }

        return this;
    }

    public string? GetString(string key)
    {
        return GetValue(GlobalStorageArea, key, StringValue) as string;
    }

    public string? GetString(string area, string key)
    {
        return GetValue(area, key, StringValue) as string;
    }

    private object? GetValue(string area, string key, string valueType)
    {
        lock (_syncDbAccess)
        {
            area = CorrectLegacyAreas(area);
            if (!DoesTableExist(area)) return null;

            using var command = new SqliteCommand($"SELECT {valueType} FROM {area} WHERE id=@id", _connection);
            command.Parameters.AddWithValue("@id", key);
            return command.ExecuteScalar();
        }
    }

    private bool DoesTableExist(string area)
    {
        using var command = new SqliteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName", _connection);
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
        lock (_syncDbAccess)
        {
            area = CorrectLegacyAreas(area);
            CreateTable(area);

            using var cmd = new SqliteCommand($"REPLACE INTO {area}(id, {rowType}) VALUES(@id, @value)", _connection);
            cmd.Parameters.AddWithValue("@id", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }
    }

    private void CreateTable(string tableName)
    {
        using var cmd = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {tableName}(id string PRIMARY KEY, {StringValue} TEXT, {BytesValue} BLOB)", _connection);
        cmd.ExecuteNonQuery();
    }

    private static string CorrectLegacyAreas(string area)
        => area.Replace(".", "_").Replace("/", "_");
}


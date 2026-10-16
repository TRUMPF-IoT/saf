// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite;
using SAF.Common;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Security", "S2077:Formatting SQL queries is security-sensitive", Justification = "Table names cannot be passed as SQL parameters; area names are inserted only through QuoteIdentifier, and all values are parameterized.")]
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
            SqliteConnection.ClearPool(_connection);
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

            using var command = new SqliteCommand($"DELETE FROM {QuoteIdentifier(area)} WHERE id=@id", _connection);
            command.Parameters.AddWithValue("@id", ToDbValue(key));
            command.ExecuteNonQuery();
        }

        return this;
    }

    public IStorageInfrastructure RemoveArea(string area)
    {
        area = CorrectLegacyAreas(area);
        if (string.Equals(area, GlobalStorageArea, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("It is not allowed to delete the global storage area.");

        lock (_syncDbAccess)
        {
            using var command = new SqliteCommand($"DROP TABLE IF EXISTS {QuoteIdentifier(area)}", _connection);
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

            using var command = new SqliteCommand($"SELECT {valueType} FROM {QuoteIdentifier(area)} WHERE id=@id", _connection);
            command.Parameters.AddWithValue("@id", ToDbValue(key));
            return command.ExecuteScalar();
        }
    }

    private bool DoesTableExist(string area)
    {
        using var command = new SqliteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName COLLATE NOCASE", _connection);
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

    private void SaveValue(string area, string key, object? value, string rowType)
    {
        lock (_syncDbAccess)
        {
            area = CorrectLegacyAreas(area);
            CreateTable(area);

            using var cmd = new SqliteCommand($"REPLACE INTO {QuoteIdentifier(area)}(id, {rowType}) VALUES(@id, @value)", _connection);
            cmd.Parameters.AddWithValue("@id", ToDbValue(key));
            cmd.Parameters.AddWithValue("@value", ToDbValue(value));
            cmd.ExecuteNonQuery();
        }
    }

    private void CreateTable(string tableName)
    {
        using var cmd = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)}(id string PRIMARY KEY, {StringValue} TEXT, {BytesValue} BLOB)", _connection);
        cmd.ExecuteNonQuery();
    }

    private static string CorrectLegacyAreas(string area)
        => area.Replace(".", "_").Replace("/", "_");

    private static object ToDbValue(object? value)
        => value ?? DBNull.Value;

    private static string QuoteIdentifier(string name)
        => $"\"{name.Replace("\"", "\"\"")}\"";
}


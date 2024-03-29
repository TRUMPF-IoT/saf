// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using SAF.Common;
using LiteDB;

namespace SAF.Storage.LiteDb;

public class Storage : IStorageInfrastructure, IDisposable
{

    private const string GlobalStorageArea = "global";
    private const string ValueKey = "value";
    private const string IdKey = "_id";
    private const string ModifiedDateKey = "modifiedDate";
    private readonly ILiteDatabase _connection;

    private readonly ReaderWriterLockSlim _syncDbAccess = new(LockRecursionPolicy.SupportsRecursion);

    public Storage(ILiteDatabase connection)
    {
        _connection = connection;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _connection.Dispose();
    }

    public byte[]? GetBytes(string key)
    {
        return Get(GlobalStorageArea, key)?.AsBinary;
    }

    public byte[]? GetBytes(string area, string key)
    {
        return Get(area, key)?.AsBinary;
    }

    public IStorageInfrastructure RemoveKey(string key)
        => RemoveKey(GlobalStorageArea, key);

    public IStorageInfrastructure RemoveKey(string area, string key)
    {
        _syncDbAccess.EnterWriteLock();
        try
        {
            GetCollection(area).Delete(key);
        }
        finally
        {
            _syncDbAccess.ExitWriteLock();
        }

        return this;
    }

    public IStorageInfrastructure RemoveArea(string area)
    {
        if (area == GlobalStorageArea)
            throw new NotSupportedException("It is not allowed to delete the global storage area.");

        _syncDbAccess.EnterWriteLock();
        try
        {
            _connection.DropCollection(ConvertAreaToCollectionName(area));
        }
        finally
        {
            _syncDbAccess.ExitWriteLock();
        }

        return this;
    }

    public string? GetString(string key)
    {
        return Get(GlobalStorageArea, key)?.AsString;
    }

    public string? GetString(string area, string key)
    {
        return Get(area, key)?.AsString;
    }

    private BsonValue? Get(string area, string key)
    {
        _syncDbAccess.EnterReadLock();
        try
        {
            var item = GetCollection(area).FindOne(Query.EQ(IdKey, key));
            if (item == null) return null;

            item.TryGetValue(ValueKey, out var value);
            return value;
        }
        finally
        {
            _syncDbAccess.ExitReadLock();
        }
    }

    public IStorageInfrastructure Set(string key, string value)
    {
        return Set(GlobalStorageArea, key, value);
    }

    public IStorageInfrastructure Set(string area, string key, string value)
    {
        return Set(area, key, (BsonValue)value);
    }

    public IStorageInfrastructure Set(string key, byte[] value)
    {
        return Set(GlobalStorageArea, key, (BsonValue)value);
    }

    public IStorageInfrastructure Set(string area, string key, byte[] value)
    {
        return Set(area, key, (BsonValue)value);
    }

    private IStorageInfrastructure Set(string area, string key, BsonValue value)
    {
        _syncDbAccess.EnterWriteLock();
        try
        {
            GetCollection(area).Upsert(
                new BsonDocument(new Dictionary<string, BsonValue>
                {
                    {IdKey, key},
                    {ValueKey, value},
                    {ModifiedDateKey, DateTime.UtcNow}
                }));

            return this;
        }
        finally
        {
            _syncDbAccess.ExitWriteLock();
        }
    }

    private ILiteCollection<BsonDocument> GetCollection(string areaName)
        => _connection.GetCollection(ConvertAreaToCollectionName(areaName));

    private static string ConvertAreaToCollectionName(string areaName)
    {
        // To ensure that LiteDb works just like RedisStorageInfrastructure and CDEStorageInfrastructure, replace '.' and '/' with an '_'. 
        // LiteDb collection names are only allowed to be [a-Z$_].
        return areaName.Replace(".", "_").Replace("/", "_");
    }
}
// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text;
using NATS.Client.ObjectStore;
using SAF.Common;

namespace SAF.Messaging.Nats;

public class Storage : IStorageInfrastructure, IDisposable
{
    private const string GlobalStorageArea = "global";
    private readonly INatsObjContext _natsObjContext;

    public Storage(INatsObjContext natsObjContext)
    {
        _natsObjContext = natsObjContext;
    }

    public IStorageInfrastructure Set(string key, string value)
    {
        return Set(GlobalStorageArea, key, value);
    }

    public IStorageInfrastructure Set(string area, string key, string value)
    {
        return Set(area, key, Encoding.UTF8.GetBytes(value));
    }

    public IStorageInfrastructure Set(string key, byte[] value)
    {
        return Set(GlobalStorageArea, key, value);
    }

    public IStorageInfrastructure Set(string area, string key, byte[] value)
    {
        var store = _natsObjContext.CreateObjectStoreAsync(area).AsTask().Result;
        _ = store.PutAsync(key, value).AsTask().Result;
        return this;
    }

    public string? GetString(string key)
    {
        return GetString(GlobalStorageArea, key);
    }

    public string? GetString(string area, string key)
    {
        var storedBytes = GetBytes(area, key);
        return storedBytes is null ? null : Encoding.UTF8.GetString(storedBytes);
    }

    public byte[]? GetBytes(string key)
    {
        return GetBytes(GlobalStorageArea, key);
    }

    public byte[]? GetBytes(string area, string key)
    {
        try
        {
            var store = _natsObjContext.CreateObjectStoreAsync(area).Result;
            return store.GetBytesAsync(key).AsTask().Result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public IStorageInfrastructure RemoveKey(string key)
    {
        return RemoveKey(GlobalStorageArea, key);
    }

    public IStorageInfrastructure RemoveKey(string area, string key)
    {
        var store = _natsObjContext.CreateObjectStoreAsync(area).Result;
        store.DeleteAsync(key).AsTask().Wait();
        return this;
    }

    public IStorageInfrastructure RemoveArea(string area)
    {
        _natsObjContext.DeleteObjectStore(area, CancellationToken.None).AsTask().Wait();
        return this;
    }

    public void Dispose()
    {
        _natsObjContext.JetStreamContext.Connection.DisposeAsync().AsTask().Wait();
    }
}

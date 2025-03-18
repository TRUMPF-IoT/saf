using System.Text;
using NATS.Client.Core;
using NATS.Client.ObjectStore;
using NATS.Net;
using SAF.Common;

namespace SAF.Messaging.Nats;

public class Storage : IStorageInfrastructure, IDisposable
{
    private readonly INatsClient _natsClient;
    private const string GlobalStorageArea = "global";
    private readonly INatsObjContext _natsObjContext;

    public Storage(INatsClient natsClient)
    {
        _natsClient = natsClient;
        _natsObjContext = natsClient.CreateObjectStoreContext();
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
        var store = _natsObjContext.CreateObjectStoreAsync(area).Result;
        store.PutAsync(key, value);
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
        var store = _natsObjContext.CreateObjectStoreAsync(area).Result;
        return store.GetBytesAsync(key).Result;
    }

    public IStorageInfrastructure RemoveKey(string key)
    {
        return RemoveKey(GlobalStorageArea, key);
    }

    public IStorageInfrastructure RemoveKey(string area, string key)
    {
        var store = _natsObjContext.CreateObjectStoreAsync(area).Result;
        store.DeleteAsync(key);
        return this;
    }

    public IStorageInfrastructure RemoveArea(string area)
    {
        _natsObjContext.DeleteObjectStore(GlobalStorageArea, CancellationToken.None);
        return this;
    }

    public void Dispose()
    {
        _natsClient.DisposeAsync();
    }
}

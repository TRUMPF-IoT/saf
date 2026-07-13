# Storage Infrastructure

SAF's storage infrastructure provides an **exchangeable key/value store** accessible to all plug-ins loaded by the same host instance. It stores both `string` and `byte[]` values, optionally namespaced by an "area".

## Interface

```csharp
namespace SAF.Common;

public interface IStorageInfrastructure
{
    // Write
    IStorageInfrastructure Set(string key, string value);
    IStorageInfrastructure Set(string area, string key, string value);
    IStorageInfrastructure Set(string key, byte[] value);
    IStorageInfrastructure Set(string area, string key, byte[] value);

    // Read
    string?  GetString(string key);
    string?  GetString(string area, string key);
    byte[]?  GetBytes(string key);
    byte[]?  GetBytes(string area, string key);

    // Delete
    IStorageInfrastructure Remove(string key);
    IStorageInfrastructure Remove(string area, string key);
    IStorageInfrastructure RemoveArea(string area);
}
```

The **area** parameter acts as a namespace. Two entries with the same key but different areas do not conflict.

---

## Available Implementations

### LiteDB

Embedded NoSQL database. Zero external dependencies, suitable for single-node deployments.

**Package:** `SAF.Storage.LiteDb`

```csharp
builder.Services.AddLiteDbStorageInfrastructure(cfg =>
{
    cfg.ConnectionString = "Filename=app.db;Mode=Shared";
});
```

`ConnectionString` follows the [LiteDB connection string format](https://www.litedb.org/docs/connection-string/).

### SQLite

Embedded relational database, using `System.Data.SQLite`.

**Package:** `SAF.Storage.SQLite`

```csharp
builder.Services.AddSQLiteStorageInfrastructure(cfg =>
{
    cfg.ConnectionString = "Data Source=app.db;Version=3;";
});
```

### Redis

Uses the same Redis connection as the messaging infrastructure. Suitable for shared state across multiple host instances.

**Package:** `SAF.Messaging.Redis`

```csharp
// Storage only
builder.Services.AddRedisStorageInfrastructure(cfg =>
{
    cfg.ConnectionString = "localhost:6379";
});

// Or combined with Redis messaging in one call
builder.Services.AddRedisInfrastructure(cfg =>
{
    cfg.ConnectionString = "localhost:6379";
});
```

### NATS (JetStream KV)

Uses NATS JetStream as a key/value backend.

**Package:** `SAF.Messaging.NATS`

```csharp
// Storage only
builder.Services.AddNatsStorageInfrastructure(cfg =>
{
    cfg.Url = "nats://localhost:4222";
});

// Or combined with NATS messaging
builder.Services.AddNatsInfrastructure(cfg =>
{
    cfg.Url = "nats://localhost:4222";
});
```

---

## How-To: Store and Retrieve Data

```csharp
public class DeviceStateService(IStorageInfrastructure storage)
{
    private const string Area = "DeviceState";

    public void SaveTemperature(string deviceId, double celsius)
    {
        storage.Set(Area, $"temperature:{deviceId}", celsius.ToString("F2"));
    }

    public double? GetTemperature(string deviceId)
    {
        var raw = storage.GetString(Area, $"temperature:{deviceId}");
        return raw is null ? null : double.Parse(raw);
    }

    public void RemoveDevice(string deviceId)
    {
        storage.Remove(Area, $"temperature:{deviceId}");
    }
}
```

The fluent API allows chaining multiple writes:

```csharp
storage
    .Set("config", "retries", "3")
    .Set("config", "timeout", "5000")
    .Set("config", "endpoint", "https://api.example.com");
```

---

## How-To: Store Binary Data

```csharp
public class BlobStore(IStorageInfrastructure storage)
{
    public void Save(string name, byte[] data) =>
        storage.Set("blobs", name, data);

    public byte[]? Load(string name) =>
        storage.GetBytes("blobs", name);
}
```

---

## How-To: Implement a Custom Storage Infrastructure

Create a class implementing `IStorageInfrastructure`:

```csharp
public class InMemoryStorage : IStorageInfrastructure
{
    private readonly Dictionary<string, string> _data = new();

    private static string Key(string area, string key) => $"{area}::{key}";

    public IStorageInfrastructure Set(string key, string value)
    {
        _data[key] = value;
        return this;
    }

    public IStorageInfrastructure Set(string area, string key, string value)
    {
        _data[Key(area, key)] = value;
        return this;
    }

    public IStorageInfrastructure Set(string key, byte[] value) =>
        Set(key, Convert.ToBase64String(value));

    public IStorageInfrastructure Set(string area, string key, byte[] value) =>
        Set(area, key, Convert.ToBase64String(value));

    public string? GetString(string key) =>
        _data.TryGetValue(key, out var v) ? v : null;

    public string? GetString(string area, string key) =>
        GetString(Key(area, key));

    public byte[]? GetBytes(string key)
    {
        var s = GetString(key);
        return s is null ? null : Convert.FromBase64String(s);
    }

    public byte[]? GetBytes(string area, string key) =>
        GetBytes(Key(area, key));

    public IStorageInfrastructure Remove(string key)
    {
        _data.Remove(key);
        return this;
    }

    public IStorageInfrastructure Remove(string area, string key) =>
        Remove(Key(area, key));

    public IStorageInfrastructure RemoveArea(string area)
    {
        var prefix = $"{area}::";
        foreach (var k in _data.Keys.Where(k => k.StartsWith(prefix)).ToList())
            _data.Remove(k);
        return this;
    }
}
```

Register it:

```csharp
builder.Services.AddSingleton<IStorageInfrastructure, InMemoryStorage>();
```

---

## Areas vs Global Keys

| Call | Effective key |
|---|---|
| `Set("mykey", value)` | `"mykey"` |
| `Set("myarea", "mykey", value)` | area-namespaced key |
| `Remove("mykey")` | removes global `"mykey"` |
| `RemoveArea("myarea")` | removes all keys in the area |

Use areas to avoid key collisions between plugins that share the same storage instance.

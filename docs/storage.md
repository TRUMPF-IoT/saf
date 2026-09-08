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

Like messaging, each storage backend is a **plug-in**: its `PluginManifest` reads a configuration section and registers `IStorageInfrastructure`. You do not register storage in host code — you deploy the plug-in DLL (add it to your plugin discovery `IncludePatterns`) and provide its configuration section. `IStorageInfrastructure` lives in `SAF.Common.dll` (a public contract assembly, added to `PluginContractsSearchPattern` automatically by `AddSafHost()`), so it is imported into every plugin container.

> Load **one** storage plug-in per host. Loading several would register competing `IStorageInfrastructure` implementations.

### LiteDB

Embedded NoSQL database. Zero external dependencies, suitable for single-node deployments.

**Plug-in DLL:** `SAF.Storage.LiteDb.dll`

```json
{
  "LiteDb": { "ConnectionString": "Filename=app.db;Mode=Shared" }
}
```

`ConnectionString` follows the [LiteDB connection string format](https://www.litedb.org/docs/connection-string/). The plug-in also accepts the legacy section name `LiteDbConfiguration`.

### SQLite

Embedded relational database, using `System.Data.SQLite`.

**Plug-in DLL:** `SAF.Storage.SQLite.dll`

```json
{
  "SQLite": { "ConnectionString": "Data Source=app.db;Version=3;" }
}
```

The plug-in also accepts the legacy section name `SQLiteConfiguration`.

### Redis

The `SAF.Messaging.Redis` plug-in registers `IStorageInfrastructure` alongside its messaging factory. Load `SAF.Messaging.Redis.dll` and configure the shared `Redis` section — the same connection backs both messaging and storage. Suitable for shared state across multiple host instances.

**Plug-in DLL:** `SAF.Messaging.Redis.dll`

```json
{
  "Redis": { "ConnectionString": "localhost:6379" }
}
```

### NATS (JetStream KV)

The `SAF.Messaging.NATS` plug-in registers NATS JetStream-backed `IStorageInfrastructure` alongside its messaging factory. Load `SAF.Messaging.NATS.dll` and configure the shared `Nats` section.

**Plug-in DLL:** `SAF.Messaging.NATS.dll`

```json
{
  "Nats": { "Url": "nats://localhost:4222" }
}
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

Register it from a plug-in's `PluginManifest`, so `IStorageInfrastructure` becomes an imported public service for all other plugins:

```csharp
public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
        => pluginServices.AddSingleton<IStorageInfrastructure, InMemoryStorage>();
}
```

Deploy the plug-in DLL by adding it to your plugin discovery `IncludePatterns`.

---

## Areas vs Global Keys

| Call | Effective key |
|---|---|
| `Set("mykey", value)` | `"mykey"` |
| `Set("myarea", "mykey", value)` | area-namespaced key |
| `Remove("mykey")` | removes global `"mykey"` |
| `RemoveArea("myarea")` | removes all keys in the area |

Use areas to avoid key collisions between plugins that share the same storage instance.

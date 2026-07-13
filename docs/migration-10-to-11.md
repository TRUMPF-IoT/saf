# Migration Guide: 10.x → 11.x

SAF 11.x is a major rewrite of the host and plugin loading infrastructure. It adopts `.NET Generic Host` patterns throughout and replaces the bespoke `ServiceCollection`-based bootstrap with a proper `IHostApplicationBuilder` integration.

This document describes every breaking change and the steps needed to migrate from SAF 10.x to SAF 11.x.

---

## Summary of Breaking Changes

| Area | Before (10.x) | After (11.x) |
|---|---|---|
| Host bootstrap | `new ServiceCollection()` + `AddHost()` + `BuildServiceProvider()` | `Host.CreateApplicationBuilder()` + `AddSafHost()` + `host.RunAsync()` |
| Plugin interface | `IServiceAssemblyManifest` with `RegisterDependencies(IServiceCollection)` | `IPluginManifest` with `ConfigureServices(IPluginSystemHostContext, IServiceCollection)` |
| Plugin lifecycle | None / manual | `IServicePlugin` / `ILifecycleServicePlugin` |
| Plugin context | Not available | `IPluginSystemHostContext` (host config, plugin config, environment) |
| Plugin settings | Single config file | Per-plugin `pluginsettings.json` resolved via `IPluginSystemHostContext` |
| C-DEngine infrastructure | `AddCdeInfrastructure()` (messaging + storage in one call) | Separate: `AddCdeMessagingInfrastructure()`, `AddCdeStorageInfrastructure()` |
| Messaging namespace | `SAF.Common.IMessagingInfrastructure` | `SAF.Messaging.Contracts.IMessagingInfrastructure` |
| Runtime plugin | Not required | `SAF.Messaging.Runtime` must be available to the plugin system; `AddSafHost()` loads it automatically as a built-in plug-in |
| `IMessagingInfrastructure` registration | Direct `IServiceCollection` singleton | Factory pattern: `IMessagingInfrastructureFactory` (keyed) + `SAF.Messaging.Runtime` resolves the primary |
| Storage namespace | `SAF.Common.IStorageInfrastructure` | Still `SAF.Common.IStorageInfrastructure` (unchanged) |
| Cross-plugin services | Not supported | `IPluginServiceProvider` |

---

## Step 1 — Update the Host Bootstrap

### Before

```csharp
var applicationServices = new ServiceCollection();
applicationServices.AddHost(config => {}, null);
applicationServices.AddCdeInfrastructure(cdeConfig =>
{
    cdeConfig.ApplicationId = "my-app";
});

using var applicationServiceProvider = applicationServices.BuildServiceProvider();
// block until shutdown...
```

### After

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddSafHost()
    .ConfigurePluginSystem(ps => ps.AddPluginAssemblyFolderContainer(options =>
    {
        options.SearchRootPath = AppContext.BaseDirectory;
        options.IncludePatterns = "MyApp.Plugin.*.dll";
    }));

// Infrastructure registered separately
builder.Services.AddCdeMessagingInfrastructure(cfg => { /* ... */ });
builder.Services.AddCdeStorageInfrastructure(cfg => { /* ... */ });

var host = builder.Build();
await host.RunAsync();
```

Configuration that was previously passed as callbacks is now in `appsettings.json`:

```json
{
  "PluginSystem": {
    "PluginSettingsRootPath": "./config",
    "PluginContractsSearchPattern": "MyApp.Contracts.dll"
  },
  "ServiceHost": {
    "Id": "node-1",
    "ServiceHostType": "MyApp"
  },
  "Messaging": {
    "PrimaryKey": "Cde"
  }
}
```

---

## Step 2 — Rename Plugin Entry Point

### Before

```csharp
using SAF.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MyPlugin;

public class ServiceAssemblyManifest : IServiceAssemblyManifest
{
    public string Name => "MyPlugin";

    public void RegisterDependencies(IServiceCollection services)
    {
        services.AddSingleton<MyService>();
    }
}
```

### After

```csharp
using SAF.PluginSystem.Hosting.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace MyPlugin;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddSingleton<MyService>();
    }
}
```

**What changed:**
- Class can have any name (the old name `ServiceAssemblyManifest` was conventional, not required).
- Interface is `IPluginManifest` instead of `IServiceAssemblyManifest`.
- Method is `ConfigureServices(IPluginSystemHostContext, IServiceCollection)` instead of `RegisterDependencies(IServiceCollection)`.
- The `Name` property is gone; the plugin is identified by its assembly name.
- Configuration is now available via `context.HostConfiguration` and `context.PluginConfiguration`.

---

## Step 3 — Migrate IMessagingInfrastructure Injection

### Before

```csharp
using SAF.Common;

public class MyService
{
    public MyService(IMessagingInfrastructure messaging) { }
}
```

### After

```csharp
using SAF.Messaging.Contracts;  // namespace changed

public class MyService
{
    public MyService(IMessagingInfrastructure messaging) { }
}
```

Update `using` directives from `SAF.Common` to `SAF.Messaging.Contracts` wherever `IMessagingInfrastructure`, `IMessageHandler`, or `Message` are referenced.

---

## Step 4 — Ensure SAF.Messaging.Runtime Is Discoverable

In 11.x the `IMessagingInfrastructure` singleton is no longer registered directly by the host. Instead, `SAF.Messaging.Runtime` acts as a plug-in that resolves and registers the primary infrastructure based on `Messaging:PrimaryKey`.

If your application uses `builder.AddSafHost()`, no extra configuration is required for the runtime plugin: `AddSafHost()` loads `SAF.Messaging.Runtime.dll` automatically as a built-in plug-in.

If you use the plugin system without `SAF.Hosting`, you must include `SAF.Messaging.Runtime.dll` in your own plugin discovery configuration.

```csharp
ps.AddPluginAssemblyFolderContainer(options =>
{
    options.SearchRootPath = AppContext.BaseDirectory;
    options.IncludePatterns = "SAF.Messaging.Runtime.dll;MyApp.Plugin.*.dll";
});
```

Without `SAF.Messaging.Runtime`, calls to inject `IMessagingInfrastructure` will throw `InvalidOperationException` at runtime.

---

## Step 5 — Migrate Plugin Lifecycle Code

### Before (manual lifecycle via constructor / background thread)

```csharp
public class MyWorker
{
    private readonly CancellationTokenSource _cts = new();

    public MyWorker(IMessagingInfrastructure messaging)
    {
        Task.Run(() => WorkLoop(_cts.Token));
    }

    private async Task WorkLoop(CancellationToken token) { /* ... */ }
}
```

### After (IServicePlugin)

```csharp
public class MyWorker(IMessagingInfrastructure messaging) : IServicePlugin
{
    private Task? _loop;
    private readonly CancellationTokenSource _cts = new();

    public Task StartAsync(CancellationToken token)
    {
        _loop = Task.Run(() => WorkLoop(_cts.Token), token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken token)
    {
        await _cts.CancelAsync();
        if (_loop is not null)
            await _loop.ConfigureAwait(false);
    }

    private async Task WorkLoop(CancellationToken token) { /* ... */ }
}
```

Register in the manifest:

```csharp
pluginServices.AddSingleton<MyWorker>();
pluginServices.AddServicePlugin<MyWorker>();
```

Or use the convenience extension:

```csharp
pluginServices.AddHostedServicePlugin<MyWorker>();
```

---

## Step 6 — Migrate Plugin-Specific Configuration

### Before

Configuration was typically read directly from `IConfiguration` injected from the host.

### After

Each plugin can have its own `pluginsettings.json` file placed in:

```
{PluginSettingsRootPath}/{AssemblyName}/pluginsettings.json
```

Access via `context.PluginConfiguration`:

```csharp
public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.Configure<MyPluginOptions>(
        context.PluginConfiguration.GetSection("MyPlugin"));
}
```

You can still read from `context.HostConfiguration` for shared/host-level configuration.

---

## Step 7 — Migrate Infrastructure Registration

### C-DEngine

| Before | After |
|---|---|
| `services.AddCdeInfrastructure(cfg => { })` | `builder.Services.AddCdeMessagingInfrastructure(cfg => { })` |
| | `builder.Services.AddCdeStorageInfrastructure(cfg => { })` |

### Redis

| Before | After |
|---|---|
| `services.AddRedisMessagingInfrastructure("localhost:6379")` | `builder.Services.AddRedisMessagingInfrastructure(cfg => cfg.ConnectionString = "localhost:6379")` |

### Storage

LiteDB and SQLite registrations are unchanged in their API shape; only ensure they are called on `builder.Services`, not on a standalone `ServiceCollection`.

---

## Step 8 — Package Reference Changes

Update your `.csproj` files:

| Old Package | New Package |
|---|---|
| `SAF.Common` (for `IMessagingInfrastructure`) | `SAF.Messaging.Contracts` |
| `SAF.Hosting` (for `IServiceAssemblyManifest`) | `SAF.PluginSystem.Hosting.Contracts` (for `IPluginManifest`) |
| `SAF.Hosting` (for host bootstrap) | `SAF.Hosting` (unchanged, but API changed) |

---

## Quick Migration Checklist

- [ ] Replace `new ServiceCollection()` + `AddHost()` with `Host.CreateApplicationBuilder()` + `AddSafHost()`
- [ ] Rename `IServiceAssemblyManifest` → `IPluginManifest`
- [ ] Rename `RegisterDependencies(IServiceCollection)` → `ConfigureServices(IPluginSystemHostContext, IServiceCollection)`
- [ ] Update `using SAF.Common` → `using SAF.Messaging.Contracts` for messaging types
- [ ] Configure `Messaging:PrimaryKey` in `appsettings.json`
- [ ] Ensure `SAF.Messaging.Runtime.dll` is discoverable by the plugin system (automatic with `AddSafHost()`)
- [ ] Replace manual lifecycle background tasks with `IServicePlugin` / `ILifecycleServicePlugin`
- [ ] Move plugin-specific config to per-plugin `pluginsettings.json` files
- [ ] Replace `AddCdeInfrastructure()` with separate `AddCdeMessagingInfrastructure()` + `AddCdeStorageInfrastructure()`

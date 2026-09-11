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
| Plugin settings | Single config file | Shared plugin settings file via `IPluginSystemHostContext.PluginConfiguration` (with host-config fallback) |
| Infrastructure registration | `AddCdeInfrastructure()` etc. on the host `IServiceCollection` | Messaging/storage are **plug-ins**; loaded by DLL discovery and selected via configuration (`Messaging:PrimaryKey`, backend sections) |
| Messaging namespace | `SAF.Common.IMessagingInfrastructure` | `SAF.Messaging.Contracts.IMessagingInfrastructure` |
| Runtime plugin | Not required | `SAF.Messaging.Runtime` must be available to the plugin system; `AddSafHost()` loads it automatically as a built-in plug-in |
| `IMessagingInfrastructure` registration | Direct `IServiceCollection` singleton | Factory pattern: a messaging plug-in registers a keyed `IMessagingInfrastructureFactory`; `SAF.Messaging.Runtime` resolves the primary via `Messaging:PrimaryKey` |
| Message handler registration in plug-ins | `IMessageHandler` interface registration often worked implicitly | Typed handlers must be registered via `SAF.Messaging.Extensions` (`AddSingletonMessageHandler<T>()` / `AddTransientMessageHandler<T>()`) and `AddMessageHandlerResolver()` |
| Storage namespace | `SAF.Common.IStorageInfrastructure` | Still `SAF.Common.IStorageInfrastructure` (unchanged) |
| Cross-plugin services | Not supported | Public contract services imported across plugin containers; `IPluginServiceProvider` for dynamic resolution |

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
        // Include your plug-ins AND the infrastructure plug-ins you want to load.
        options.IncludePatterns = "MyApp.Plugin.*.dll;SAF.Messaging.Cde.dll";
    }));

var host = builder.Build();
await host.RunAsync();
```

Infrastructure is **not** registered in host code anymore. Instead you deploy the infrastructure plug-in DLLs and select/configure them through configuration (`appsettings.json` or the plugin settings file):

```json
{
  "PluginSystem": {
    "PluginSettingsRootPath": ".",
    "PluginSettingsFilePath": "./pluginsettings.json"
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

If you use typed subscriptions (`Subscribe<TMessageHandler>()`), also migrate the handler registration pattern in your plugin manifest:

```csharp
using SAF.Messaging.Extensions;

public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.AddSingletonMessageHandler<MyMessageHandler>();
    // or pluginServices.AddTransientMessageHandler<MyMessageHandler>();

    pluginServices.AddMessageHandlerResolver();
}
```

Do not register handlers only as `IMessageHandler` (for example `AddSingleton<IMessageHandler, MyMessageHandler>()`).
The SAF messaging runtime resolves handlers by concrete type; interface-only registrations are not resolved.

---

## Step 3a — Replace TestableIO with Testably for File System Abstractions

If your solution still references `TestableIO.System.IO.Abstractions.*`, migrate to `Testably.Abstractions` to stay aligned with current SAF packages.

### Package migration

- Replace `TestableIO.System.IO.Abstractions.Wrappers` with `Testably.Abstractions`
- Replace `TestableIO.System.IO.Abstractions.TestingHelpers` with `Testably.Abstractions.Testing`

### Runtime DI migration

Use `RealFileSystem` as the concrete `IFileSystem` registration:

```csharp
services.TryAddTransient<IFileSystem, RealFileSystem>();
```

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
pluginServices.AddServicePlugin<MyWorker>();
```

If other services in the same plug-in container also need `MyWorker` by its concrete type, additionally register `pluginServices.AddSingleton<MyWorker>()`.

---

## Step 6 — Migrate Plugin-Specific Configuration

### Before

Configuration was typically read directly from `IConfiguration` injected from the host.

### After

All plugins share a single plugin settings file, resolved from `{PluginSettingsRootPath}/{PluginSettingsFilePath}` (defaults: `./config` and `./pluginsettings.json`) plus an optional `{file}.{EnvironmentName}.json` overlay. Each plugin reads its own top-level section. It is exposed as `context.PluginConfiguration`:

```csharp
public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.Configure<MyPluginOptions>(
        context.PluginConfiguration.GetSection("MyPlugin"));
}
```

You can still read from `context.HostConfiguration` for shared/host-level configuration. In the simplest setups, point `PluginSettingsFilePath` at the host's `appsettings.json` and keep everything in one file.

---

## Step 7 — Deploy Infrastructure as Plug-ins

In 10.x you registered messaging and storage on the host `IServiceCollection` (e.g. `AddCdeInfrastructure()`, `AddRedisMessagingInfrastructure()`). In 11.x each infrastructure is a **plug-in**; the `Add*Infrastructure` extension methods are called by the plug-in's own `PluginManifest`, not by your host.

To migrate, for each infrastructure you used:

1. **Deploy the plug-in DLL** and make it discoverable via a plugin folder container's `IncludePatterns` (e.g. `SAF.Messaging.Cde.dll`, `SAF.Messaging.Redis.dll`, `SAF.Storage.LiteDb.dll`).
2. **Select the messaging backend** with `Messaging:PrimaryKey` (`Cde`, `Redis`, `Nats`, `InProcess`, `Routing`).
3. **Provide the backend's configuration section** (`Cde`, `Redis`, `Nats`, `LiteDb`, `SQLite`, `MessageRouting`).

| 10.x host call | 11.x plug-in + configuration |
|---|---|
| `services.AddCdeInfrastructure(...)` | Load `SAF.Messaging.Cde.dll`; `Messaging:PrimaryKey = "Cde"`; `Cde` section |
| `services.AddRedisInfrastructure(...)` | Load `SAF.Messaging.Redis.dll`; `Messaging:PrimaryKey = "Redis"`; `Redis` section (provides messaging **and** storage) |
| `services.AddLiteDbStorageInfrastructure(...)` | Load `SAF.Storage.LiteDb.dll`; `LiteDb` section |
| `services.AddSQLiteStorageInfrastructure(...)` | Load `SAF.Storage.SQLite.dll`; `SQLite` section |

See [Messaging Infrastructure](./messaging.md) and [Storage Infrastructure](./storage.md) for the exact configuration sections.

---

## Step 8 — Package Reference Changes

Update your `.csproj` files:

| Old Package | New Package |
|---|---|
| `SAF.Common` (for `IMessagingInfrastructure`) | `SAF.Messaging.Contracts` |
| `SAF.Hosting` (for `IServiceAssemblyManifest`) | `SAF.PluginSystem.Hosting.Contracts` (for `IPluginManifest`) |
| `SAF.Hosting` (for host bootstrap) | `SAF.Hosting` (unchanged, but API changed) |
| `IMessageHandler` plugin registration without extensions | Add package `SAF.Messaging.Extensions` and use `AddSingletonMessageHandler<T>()` / `AddTransientMessageHandler<T>()` + `AddMessageHandlerResolver()` |

### Plugin assembly validation is a separate package

Plugin assembly validation is opt-in and lives in `SAF.PluginSystem.Hosting.Extensions`. `SAF.PluginSystem.Hosting` does not reference it, so the core engine does not pull the validators and their cryptography dependencies into applications that load plug-ins without validating them.

To use `AddStrongNamePluginAssemblyValidator`, `AddDigitalSignaturePluginAssemblyValidator` or your own `IPluginAssemblyValidator`, reference the package explicitly:

```xml
<PackageReference Include="SAF.PluginSystem.Hosting.Extensions" />
```

That package references `System.Security.Cryptography.Pkcs` for Authenticode CMS signature parsing, which it brings along transitively.

### `PluginAssemblyFolderContainer` constructor

`IEnumerable<IPluginAssemblyValidator> assemblyValidators` was added as the **last** constructor parameter, after `IFileSystem fileSystem`, and is required. Code that constructs the container directly instead of using `AddPluginAssemblyFolderContainer` must pass a sequence — an empty one keeps the 10.x behaviour of loading without validation:

```csharp
new PluginAssemblyFolderContainer(loggerFactory, manifestLoader, options, fileSystem, []);
```

This is an intentional break. It is a compile error rather than a silent behaviour change, which is the point: a container built with a stale call would otherwise load plug-ins with validators that were configured but never consulted.

### NATS messaging keeps blocking backpressure

`SAF.Messaging.Nats` now builds on **NATS.Net 3.x**, which stopped forcing
`SubPendingChannelFullMode = BoundedChannelFullMode.Wait` inside the `NatsClient` constructor; the
`NatsOpts` default is `DropNewest`. SAF sets `Wait` explicitly, so a subscription whose `IMessageHandler`
is slower than the publish rate still applies backpressure to the reader instead of silently discarding
messages — the 10.x behaviour. No action is required; the note is here because the underlying default
inverted, so a host that builds its own `NatsOpts` has to set the mode itself.

### Digital-signature validation is secure by default

`DigitalSignaturePluginAssemblyValidatorOptions.RequireValidDigitalSignature` defaults to `true`, so registering the validator without configuration demands a signature that is intact, covers the file and chains to a trusted root. Check that against the signatures your plug-ins actually carry before enabling the validator: unsigned plug-ins, and plug-ins whose signer chains to a root the host does not trust, are skipped with a warning.

Switching the requirement off is only meaningful together with `AllowedSignerThumbprints`, which still requires a signature covering the file and only skips the trust chain. Switching off both is refused: the host fails to start with an `OptionsValidationException` instead of registering a validator that checks nothing.

`DigitalSignaturePluginAssemblyValidator` is constructed by `AddDigitalSignaturePluginAssemblyValidator` only; it has no public constructor, and registering it as a plain service type (`AddPluginAssemblyValidator<DigitalSignaturePluginAssemblyValidator>()`) fails when the service provider resolves it.

### Register forwarded host services with `AddHostServiceForwarder<T>()`

SAF 10.x had a single, shared `ServiceCollection` — every plug-in saw every host service directly. In
11.x each plug-in loads into its own isolated container, so a host service now reaches a plug-in only if
you forward it explicitly:

```csharp
services.AddSingleton<MySharedSingleton>();
services.AddHostServiceForwarder<MySharedSingleton>();
```

`AddHostServiceForwarder<T>()` registers two things together, and both are required: a forwarder that
bridges the already-resolved host instance into each plug-in container, and an `ISharedAssemblySource`
that puts `T`'s declaring assembly into the plugin system's
[shared set](./plugin-system.md#the-shared-set) — without it, each plug-in would load its own copy of the
contract assembly and fail to resolve the forwarded instance.

`AddSecretStore()` and `AddSafHost()` already do this for `ISecretStore` and `IServiceHostInfo`, so no
action is needed for either. For a custom `IHostServiceForwarder` implementation, see
[IHostServiceForwarder](./plugin-system.md#ihostserviceforwarder).

---

## One Plug-in's Shared-Assembly Conflict Fails the Whole Host

Rebuilding every plug-in against v11 is already required for reasons covered elsewhere in this guide — `IPluginManifest`, `ConfigureServices`, the `SAF.Messaging.Contracts` namespace, and so on. A plug-in that still targets the old API does not implement `IPluginManifest` at all, so it is simply skipped with a log entry; it does not stop the host.

What is easy to miss is what happens **after** a plug-in has been rebuilt against v11's contracts, if it — or one of its own dependencies — still pins an older major version of an assembly the host shares. That is not just a problem for the one plug-in: with the default `SharedAssemblyConflictBehavior.Fail`, `PluginAssemblyFolderContainer` queues the conflict and throws once loading finishes, and that exception propagates out of the whole plugin system, so **the v11 host does not start** — every other plug-in included. See [the shared set](./plugin-system.md#the-shared-set) and [version handling](./plugin-system.md#version-handling) for the full mechanism.

The [shared set](./plugin-system.md#the-shared-set) includes `SAF.PluginSystem.Hosting.Contracts` and `SAF.Common` (both now `AssemblyVersion=11.0.0.0`), plus the `Microsoft.Extensions.*`/`System.IO.Abstractions` assemblies the plugin system forces across the boundary. A plug-in built correctly against `IPluginManifest` can still hit the conflict this way — for example, if one of *its own* dependencies still pins an older major of `Microsoft.Extensions.*`, that is the same disallowed roll-forward as an unported SAF contract reference, and it takes the whole host down just the same.

**Required:** rebuild the plug-in, or update the outdated dependency, against v11.

**If that is not possible right now:**

- Set `PluginSystemOptions.AllowMajorVersionRollForward = true`. This is global — it also removes the major-version protection for your *own* contract assemblies, not only for the dependency causing the immediate failure.
- Or set `SharedAssemblyConflictBehavior = SharedAssemblyConflictBehavior.IsolateWithWarning`. The plug-in starts, but at a cost: types of the conflicting assembly no longer cross the plug-in boundary, so instances the plug-in constructs and instances the host constructs are no longer type-compatible.

---

## Quick Migration Checklist

- [ ] Rebuild plug-ins (and check their dependencies) against v11 — one outdated shared-assembly reference fails the whole host's startup, not just that plug-in
- [ ] Replace `new ServiceCollection()` + `AddHost()` with `Host.CreateApplicationBuilder()` + `AddSafHost()`
- [ ] Rename `IServiceAssemblyManifest` → `IPluginManifest`
- [ ] Rename `RegisterDependencies(IServiceCollection)` → `ConfigureServices(IPluginSystemHostContext, IServiceCollection)`
- [ ] Update `using SAF.Common` → `using SAF.Messaging.Contracts` for messaging types
- [ ] Register typed message handlers in each plug-in via `SAF.Messaging.Extensions` (`AddSingletonMessageHandler<T>()` / `AddTransientMessageHandler<T>()`) and call `AddMessageHandlerResolver()`
- [ ] Configure `Messaging:PrimaryKey` in configuration
- [ ] Ensure `SAF.Messaging.Runtime.dll` is discoverable by the plugin system (automatic with `AddSafHost()`)
- [ ] Replace manual lifecycle background tasks with `IServicePlugin` / `ILifecycleServicePlugin` registered via `AddServicePlugin<T>()`
- [ ] Move plugin configuration into the shared plugin settings file (or host `appsettings.json`) under a per-plugin section
- [ ] Deploy messaging/storage as plug-ins (add their DLLs to `IncludePatterns`) instead of calling `Add*Infrastructure()` on the host
- [ ] Reference `SAF.PluginSystem.Hosting.Extensions` explicitly if you use plugin assembly validation, and check the `RequireValidDigitalSignature = true` default against the signatures your plug-ins actually carry
- [ ] Forward any additional host service your plug-ins need with `AddHostServiceForwarder<T>()` — v10's single shared container needed no such step

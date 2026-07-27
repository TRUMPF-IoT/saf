# Plugin System

The plugin system (`SAF.PluginSystem.*`) is a **SAF-independent** assembly loading engine that:

- Scans directories for assemblies containing `IPluginManifest` implementations
- Creates an **isolated `IServiceProvider`** for each discovered plug-in
- Forwards a set of **shared services** (from the host container) into every plugin container
- Manages the lifecycle of `IServicePlugin` and `ILifecycleServicePlugin` implementations
- Makes **public contract services** available for constructor injection in every plugin container
- Provides `IPluginServiceProvider` as a fallback for dynamic/late-bound cross-plugin resolution

SAF uses the plugin system as its foundation and adds messaging, storage, and host-info on top, but the plugin system itself has no dependency on SAF.

---

## Core Concepts

### IPluginManifest

Every plug-in assembly should contain **exactly one** concrete class implementing `IPluginManifest`. This is the single entry point the plugin system uses to configure the plug-in's DI container.

During discovery, the plugin loader instantiates the first concrete, non-abstract `IPluginManifest` implementation it finds and ignores assemblies that only contain the interface or other non-instantiable types.

```csharp
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

namespace MyPlugin;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        // Register services into the plugin's own isolated DI container
        pluginServices.AddSingleton<MyPrivateService>();
        pluginServices.AddSingleton<IMyPublicContract, MyPublicService>();

        // Register a lifecycle participant
        pluginServices.AddServicePlugin<MyBackgroundWorker>();
    }
}
```

The `IPluginSystemHostContext` provides:

| Member | Type | Description |
|---|---|---|
| `Environment` | `IPluginSystemHostEnvironment` | Environment name, plugin settings root path |
| `HostConfiguration` | `IConfiguration` | Host-wide config (e.g. `appsettings.json`) |
| `PluginConfiguration` | `IConfiguration` | Plugin-specific config (e.g. `pluginsettings.json`) |

### IServicePlugin

Implement `IServicePlugin` to participate in the host's start/stop lifecycle. Register via `AddServicePlugin<T>()`:

```csharp
using SAF.PluginSystem.Hosting.Contracts;

namespace MyPlugin;

public class MyBackgroundWorker(IMessagingInfrastructure messaging) : IServicePlugin
{
    private object? _subscription;

    public Task StartAsync(CancellationToken token)
    {
        _subscription = messaging.Subscribe("my/topic", msg => Handle(msg));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        if (_subscription is not null)
            messaging.Unsubscribe(_subscription);
        return Task.CompletedTask;
    }

    private void Handle(Message msg) { /* ... */ }
}
```

### ILifecycleServicePlugin

For phased initialization, implement `ILifecycleServicePlugin` (extends `IServicePlugin` with `StartingAsync`, `StartedAsync`, `StoppingAsync`, `StoppedAsync`):

```csharp
public class MyLifecyclePlugin : ILifecycleServicePlugin
{
    public Task StartingAsync(CancellationToken token)
    {
        // Called before StartAsync — open connections, acquire resources
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken token)
    {
        // Main start logic
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken token)
    {
        // Called after StartAsync — announce readiness
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken token)
    {
        // Called before StopAsync — begin graceful shutdown
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        // Main stop logic
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken token)
    {
        // Called after StopAsync — release resources
        return Task.CompletedTask;
    }
}
```

The lifecycle order follows `IHostedLifecycleService` semantics, executed sequentially across all plugins:

```
StartingAsync → StartAsync → StartedAsync
StoppingAsync → StopAsync  → StoppedAsync
```

---

## DI Containers

The plugin system maintains one DI container per plugin. The host forwards a fixed set of common services into every plugin container, while public contract services registered by one plugin are imported into the others.

```mermaid
graph TB
    subgraph HOST["Host Container (IHostApplicationBuilder.Services)"]
        LOG[ILoggerFactory]
        CFG[IConfiguration]
        FS[IFileSystem]
        PSP[IPluginServiceProvider]
        ENV[IPluginSystemHostEnvironment]
    end

    subgraph MP["Messaging/Storage Plugin Containers"]
        MSG["IMessagingInfrastructure\n(via Runtime plugin)"]
        STO[IStorageInfrastructure]
    end

    subgraph PA["Plugin A Container"]
        MSG_A[IMessagingInfrastructure ←imported]
        STO_A[IStorageInfrastructure ←imported]
        A_PRIV[PrivateServiceA]
        A_PUB[IMyContract → MyServiceA]
    end

    subgraph PB["Plugin B Container"]
        MSG_B[IMessagingInfrastructure ←imported]
        STO_B[IStorageInfrastructure ←imported]
        B_PRIV[PrivateServiceB]
    end

    HOST -->|forwarded| PA
    HOST -->|forwarded| PB

    MSG -->|imported public| MSG_A
    MSG -->|imported public| MSG_B
    STO -->|imported public| STO_A
    STO -->|imported public| STO_B

    A_PUB -->|imported public| PB
```

> `IMessagingInfrastructure` and `IStorageInfrastructure` are ordinary public contract services provided by the messaging/storage plug-ins — they reach other plugin containers through the same import mechanism as any application contract, not through host forwarding.


**Key rules:**

- Services registered in a plugin container are **private by default** — other plugins cannot resolve them unless they are registered against a **publicly accessible interface** (an interface in a shared "contracts" assembly).
- Public contract services registered in any plugin container are **injected into every other plugin container** automatically — consume them via normal constructor injection.
- `IPluginServiceProvider` is available as a fallback for dynamic or late-bound scenarios but is a Service Locator — prefer constructor injection.
- If multiple plugins register the same interface and `IPluginServiceProvider` is used, `GetService<T>()` throws; use `GetServices<T>()` instead.
- Host services are **not** forwarded automatically. Only the services listed below and any `IHostServiceForwarder` registrations are bridged into plugin containers.

### Services forwarded into every plugin container

| Service | Notes |
|---|---|
| `ILoggerFactory` / `ILogger<T>` | Shared logging pipeline |
| `IPluginServiceProvider` | Cross-plugin service resolution |
| `IPluginSystemHostEnvironment` | Environment name, settings root path |
| `IFileSystem` | Abstracted file system |

Additional services can be bridged explicitly via `IHostServiceForwarder` (see below).

### IHostServiceForwarder

To forward an additional host service into every plugin container, register a `HostServiceForwarder<T>` in the host's `IServiceCollection`:

```csharp
// Register the service in the host container
services.AddSingleton<MySharedService>();

// Bridge it into every plugin container
services.AddSingleton<IHostServiceForwarder, HostServiceForwarder<MySharedService>>();
```

`HostServiceForwarder<T>` is resolved from the host container (receiving the already-built singleton via constructor injection) and calls `pluginServices.AddSingleton(instance)` for each plugin — one shared instance, no factory, no service locator.

Implement `IHostServiceForwarder` directly for more control, e.g. to register a service under a different interface:

```csharp
public sealed class MyForwarder(MySharedService service) : IHostServiceForwarder
{
    public void Forward(IServiceCollection pluginServices)
        => pluginServices.AddSingleton<IMyContract>(service);
}
```

---

## Cross-Plugin Services

### Messaging Handlers in Plug-ins (Required)

If a plug-in uses typed messaging subscriptions (`Subscribe<TMessageHandler>()`), the handler type **must** be registered via the extensions from `SAF.Messaging.Extensions`.

Do **not** register message handlers only as `IMessageHandler` (for example `AddSingleton<IMessageHandler, MyHandler>()`).
The SAF messaging runtime resolves handlers by their concrete type. Interface-only registrations are not resolved.

Required setup in the plug-in manifest:

```csharp
using SAF.Messaging.Extensions;

public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.AddSingletonMessageHandler<MyHandler>();
    // or pluginServices.AddTransientMessageHandler<MyHandler>();

    pluginServices.AddMessageHandlerResolver();
}
```

If this is not configured, typed handlers will not be resolved by SAF's messaging system.

### Registering a Public Service

In your plugin manifest, register the service against a **contract interface** defined in a shared assembly:

```csharp
// MyApp.Contracts assembly (referenced by both plugins and the host)
public interface IOrderService
{
    IEnumerable<Order> GetPending();
}
```

```csharp
// Plugin A manifest
pluginServices.AddSingleton<IOrderService, OrderServiceImpl>();
```

### Consuming a Public Service from Another Plugin

The plugin system automatically makes public contract services available in every plugin container. Consume them via **constructor injection** — no service locator needed:

```csharp
// Plugin B — IOrderService is injected directly from Plugin A's registration
public class OrderConsumer(IOrderService orderService)
{
    public void ProcessOrders()
    {
        var orders = orderService.GetPending();
        // ...
    }
}
```

> The host must make the contracts assembly discoverable through `PluginContractsSearchPattern` so the plugin system can recognise the public types. When you use `AddSafHost()`, SAF's own contract assemblies are already included and your configured patterns only need to cover additional application-specific contracts.

### IPluginServiceProvider (fallback only)

For dynamic or late-bound scenarios where the service type is not known at compile time, `IPluginServiceProvider` is available. Prefer constructor injection whenever possible — `IPluginServiceProvider` is a Service Locator.

```csharp
// Use only when constructor injection is not applicable
public class DynamicConsumer(IPluginServiceProvider pluginServices)
{
    public void ProcessOrders()
    {
        var orders = pluginServices.GetService<IOrderService>();
        // ...
    }
}
```

---

## Using the Plugin System Without SAF

The plugin system has no dependency on SAF infrastructure. You can use it with a plain .NET host:

```csharp
var builder = Host.CreateApplicationBuilder(args);

var pluginSystemBuilder = builder.AddPluginSystem(options =>
{
    options.PluginSettingsRootPath = "./config";
});

pluginSystemBuilder.AddPluginAssemblyFolderContainer(options =>
{
    options.SearchRootPath = AppContext.BaseDirectory;
    options.IncludePatterns = "MyApp.Plugin.*.dll";
});

// Register host services that should be forwarded into every plugin container
builder.Services.AddSingleton<IMySharedService, MySharedService>();
builder.Services.AddSingleton<IHostServiceForwarder, HostServiceForwarder<IMySharedService>>();

var host = builder.Build();
await host.RunAsync();
```

Services are **not** forwarded into plugin containers automatically. Use `IHostServiceForwarder` / `HostServiceForwarder<T>` to bridge specific host services explicitly.

---

## Plugin Settings

All plugins share a single plugin settings file, exposed to each manifest as `context.PluginConfiguration`. The plugin system loads it from:

```
{PluginSettingsRootPath}/{PluginSettingsFilePath}
```

with `PluginSettingsRootPath` defaulting to `.` and `PluginSettingsFilePath` defaulting to `./pluginsettings.json`. An optional environment-specific overlay named `{file}.{EnvironmentName}.json` (e.g. `pluginsettings.Development.json`) is layered on top if present. Both paths come from the `PluginSystem` configuration section.

Because the file is shared, plugins keep their settings under distinct top-level sections (the built-in messaging/storage plugins use `Messaging`, `Redis`, `Nats`, `LiteDb`, `SQLite`, `MessageRouting`).

Access your section via `context.PluginConfiguration` in `ConfigureServices`:

```csharp
public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.Configure<MyPluginOptions>(
        context.PluginConfiguration.GetSection("MyPlugin"));
}
```

Example `pluginsettings.json`:

```json
{
  "MyPlugin": {
    "IntervalSeconds": 10,
    "Topic": "myapp/events/ping"
  }
}
```

> A plugin may also fall back to host configuration (`context.HostConfiguration`) — the built-in plugins read their section from `PluginConfiguration` first and fall back to `HostConfiguration`. In the simplest setups you can point `PluginSettingsFilePath` at the host's `appsettings.json` and keep everything in one file.


---

## Lifecycle Sequence Diagram

```mermaid
sequenceDiagram
    participant Host as .NET Generic Host
    participant SPH as ServicePluginHost
    participant PA  as Plugin A (IServicePlugin)
    participant PB  as Plugin B (ILifecycleServicePlugin)

    Host->>SPH: StartingAsync
    SPH->>PB: StartingAsync

    Host->>SPH: StartAsync
    SPH->>PA: StartAsync
    SPH->>PB: StartAsync

    Host->>SPH: StartedAsync
    SPH->>PB: StartedAsync

    Note over Host: Application running...

    Host->>SPH: StoppingAsync
    SPH->>PB: StoppingAsync

    Host->>SPH: StopAsync
    SPH->>PA: StopAsync
    SPH->>PB: StopAsync

    Host->>SPH: StoppedAsync
    SPH->>PB: StoppedAsync
```

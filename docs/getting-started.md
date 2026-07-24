# Getting Started with SAF

This guide walks you through creating a minimal SAF application from scratch: a host process with two plug-ins that communicate via pub/sub messaging.

## Prerequisites

- .NET 10 SDK
- Familiarity with `Microsoft.Extensions.Hosting` and dependency injection

## NuGet Packages

Reference the messaging/storage implementations in your **host** project. They are deployed as **plug-in assemblies** (their DLLs must end up next to the host binary so the plugin system can discover them) — you do **not** register them directly in code:

```xml
<PackageReference Include="SAF.Hosting" />
<PackageReference Include="SAF.Messaging.InProcess" />
<PackageReference Include="SAF.Storage.LiteDb" />
```

> `SAF.Messaging.Runtime` is pulled in transitively and auto-loaded by `AddSafHost` — you don't need to reference or configure it explicitly.

Plug-in projects reference only the contracts:

```xml
<PackageReference Include="SAF.PluginSystem.Hosting.Contracts" />
<PackageReference Include="SAF.Messaging.Contracts" />
<PackageReference Include="SAF.Common" />
```

---

## Step 1 — Create the Host

Create a console application `MyApp.Host` and configure the SAF host. Infrastructure is **not** registered in code — it is loaded as plug-ins and selected via configuration:

```csharp
// Program.cs
using Microsoft.Extensions.Hosting;
using SAF.Hosting;
using SAF.PluginSystem.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add the SAF host. It binds the "PluginSystem" and "ServiceHost" configuration sections,
// auto-loads SAF.Messaging.Runtime.dll, and adds SAF.Common.dll + SAF.Messaging.Contracts.dll
// to the plugin contracts search pattern automatically.
builder.AddSafHost()
    // Tell the plugin system which assemblies to scan for IPluginManifest implementations.
    // Include your own plug-ins AND the messaging/storage implementation plug-ins.
    .ConfigurePluginSystem(ps => ps.AddPluginAssemblyFolderContainer(options =>
    {
        options.SearchRootPath = AppContext.BaseDirectory;
        options.IncludePatterns =
            "MyApp.Plugin.*.dll;SAF.Messaging.InProcess.dll;SAF.Storage.LiteDb.dll";
        options.Recursive = false;
    }));

var host = builder.Build();
await host.RunAsync();
```

### appsettings.json

```json
{
  "PluginSystem": {
    "PluginSettingsRootPath": ".",
    "PluginSettingsFilePath": "./pluginsettings.json"
  },
  "ServiceHost": {
    "Id": "my-app-node-1",
    "ServiceHostType": "MyApp",
    "FileSystemUserBasePath": "tempfs"
  },
  "Messaging": {
    "PrimaryKey": "InProcess"
  },
  "LiteDb": {
    "ConnectionString": "Filename=app.db;Mode=Shared"
  }
}
```

> **How infrastructure gets wired:**
> - The `SAF.Messaging.InProcess` plug-in registers a keyed `IMessagingInfrastructureFactory` under the key `"InProcess"`.
> - The auto-loaded `SAF.Messaging.Runtime` plug-in reads `Messaging:PrimaryKey` and exposes the matching factory's output as the `IMessagingInfrastructure` that plug-ins inject.
> - The `SAF.Storage.LiteDb` plug-in reads the `LiteDb` section and registers `IStorageInfrastructure`.
>
> `Messaging:PrimaryKey` must match a loaded messaging plug-in's key: `InProcess`, `Redis`, `Nats`, `Cde`, or `Routing`.

---

## Step 2 — Create a Publisher Plug-in

Create a class library `MyApp.Plugin.Publisher`:

```csharp
// PluginManifest.cs
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

namespace MyApp.Plugin.Publisher;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddServicePlugin<PublisherService>();
    }
}
```

```csharp
// PublisherService.cs
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

namespace MyApp.Plugin.Publisher;

public class PublisherService(IMessagingInfrastructure messaging) : IServicePlugin
{
    public async Task StartAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            messaging.Publish(new Message
            {
                Topic = "myapp/events/ping",
                Payload = $"{{\"time\":\"{DateTimeOffset.UtcNow:O}\"}}"
            });

            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
```

> `AddServicePlugin<T>` (from `SAF.PluginSystem.Hosting.Contracts`) registers `T` as an `IServicePlugin`, so the plugin system starts and stops it automatically. If other services in the same plug-in container also need `T` by its concrete type, register it additionally with `AddSingleton<T>()`.

---

## Step 3 — Create a Subscriber Plug-in

Create a class library `MyApp.Plugin.Subscriber`:

```csharp
// PluginManifest.cs
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

namespace MyApp.Plugin.Subscriber;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddServicePlugin<SubscriberService>();
    }
}
```

```csharp
// SubscriberService.cs
using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

namespace MyApp.Plugin.Subscriber;

public class SubscriberService(
    IMessagingInfrastructure messaging,
    ILogger<SubscriberService> logger) : IServicePlugin
{
    private object? _subscription;

    public Task StartAsync(CancellationToken token)
    {
        // Subscribe to all topics matching the regex pattern
        _subscription = messaging.Subscribe(
            routeFilterPattern: "myapp/events/.*",
            handler: msg => logger.LogInformation("Received on {Topic}: {Payload}", msg.Topic, msg.Payload));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        if (_subscription is not null)
            messaging.Unsubscribe(_subscription);

        return Task.CompletedTask;
    }
}
```

---

## Step 4 — Project Layout

```
MyApp/
├── MyApp.Host/
│   ├── MyApp.Host.csproj
│   ├── Program.cs
│   └── appsettings.json
├── MyApp.Plugin.Publisher/
│   ├── MyApp.Plugin.Publisher.csproj
│   ├── PluginManifest.cs
│   └── PublisherService.cs
└── MyApp.Plugin.Subscriber/
    ├── MyApp.Plugin.Subscriber.csproj
    ├── PluginManifest.cs
    └── SubscriberService.cs
```

Build and publish the plug-in assemblies next to the host binary. The `IncludePatterns` setting controls which DLLs are scanned for `IPluginManifest` implementations. Remember this must also include the messaging and storage implementation DLLs (`SAF.Messaging.InProcess.dll`, `SAF.Storage.LiteDb.dll`) since those are plug-ins too. `SAF.Messaging.Runtime.dll` is added automatically by `AddSafHost`.

---

## Step 5 — Run

```bash
dotnet run --project MyApp.Host
```

You should see log output from the subscriber each time the publisher fires:

```
info: MyApp.Plugin.Subscriber.SubscriberService[0]
      Received on myapp/events/ping: {"time":"2025-07-13T10:00:00.000Z"}
```

---

## Next Steps

- [SAF Host](./saf-host.md) — advanced host configuration
- [Messaging Infrastructure](./messaging.md) — switch to Redis or NATS
- [Storage Infrastructure](./storage.md) — persist data with LiteDB or SQLite
- [Plugin System](./plugin-system.md) — understand plug-in isolation and cross-plug-in services
- [Toolbox Services](./toolbox.md) — Heartbeat, Request/Reply, File Transfer

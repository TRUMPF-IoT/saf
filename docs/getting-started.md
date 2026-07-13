# Getting Started with SAF

This guide walks you through creating a minimal SAF application from scratch: a host process with two plug-ins that communicate via pub/sub messaging.

## Prerequisites

- .NET 10 SDK
- Familiarity with `Microsoft.Extensions.Hosting` and dependency injection

## NuGet Packages

Add the following packages to your host project:

```xml
<PackageReference Include="SAF.Hosting" />
<PackageReference Include="SAF.Messaging.InProcess" />
<PackageReference Include="SAF.Messaging.Runtime" />
<PackageReference Include="SAF.Storage.LiteDb" />
```

Plug-in projects reference only the contracts:

```xml
<PackageReference Include="SAF.PluginSystem.Hosting.Contracts" />
<PackageReference Include="SAF.Messaging.Contracts" />
<PackageReference Include="SAF.Common" />
```

---

## Step 1 — Create the Host

Create a console application `MyApp.Host` and configure the SAF host:

```csharp
// Program.cs
using SAF.Hosting;
using SAF.Messaging.InProcess;
using SAF.Messaging.Runtime;
using SAF.Storage.LiteDb;

var builder = Host.CreateApplicationBuilder(args);

// 1. Add the SAF host. It reads plugin settings from appsettings.json ("PluginSystem" section).
builder.AddSafHost()
    // 2. Tell the plugin system where to look for plug-in assemblies.
    .ConfigurePluginSystem(ps => ps.AddPluginAssemblyFolderContainer(options =>
    {
        options.SearchRootPath = AppContext.BaseDirectory;
        options.IncludePatterns = "MyApp.Plugin.*.dll";
        options.Recursive = false;
    }));

// 3. Register messaging infrastructure (in-process for development).
builder.Services.AddInProcessMessagingInfrastructure();

// 4. Register storage infrastructure.
builder.Services.AddLiteDbStorageInfrastructure(cfg =>
    cfg.ConnectionString = "Filename=app.db;Mode=Shared");

var host = builder.Build();
await host.RunAsync();
```

### appsettings.json

```json
{
  "PluginSystem": {
    "PluginSettingsRootPath": "./config",
    "PluginSettingsFilePath": "./pluginsettings.json",
    "PluginContractsSearchPattern": "MyApp.Contracts.dll"
  },
  "ServiceHost": {
    "Id": "my-app-node-1",
    "ServiceHostType": "MyApp",
    "FileSystemUserBasePath": "tempfs"
  },
  "Messaging": {
    "PrimaryKey": "InProcess"
  }
}
```

> **Note:** `Messaging:PrimaryKey` tells `SAF.Messaging.Runtime` which registered messaging factory to use as the primary `IMessagingInfrastructure`. The value must match a key registered by one of the `Add*MessagingInfrastructure` calls (`InProcess`, `Redis`, `Nats`, `Routing`, …).

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
        pluginServices.AddSingleton<PublisherService>();
        pluginServices.AddHostedServicePlugin<PublisherService>();
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

> `AddHostedServicePlugin<T>` (from `SAF.PluginSystem.Hosting.Extensions`) is a convenience method that registers `T` both as itself and as `IServicePlugin`, so the plugin system starts and stops it automatically.

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
        pluginServices.AddSingleton<SubscriberService>();
        pluginServices.AddHostedServicePlugin<SubscriberService>();
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

Build and publish the plug-in assemblies next to the host binary (or into a subfolder). The `IncludePatterns` setting controls which DLLs are scanned for `IPluginManifest` implementations.

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

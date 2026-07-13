# SAF Host

The SAF Host is the top-level composition root for a SAF application. It wraps .NET's `IHostApplicationBuilder` (i.e. `Host.CreateApplicationBuilder`) and wires together the plugin system, service host info, infrastructure registrations, and optional diagnostics.

## How It Fits Together

```mermaid
graph LR
    AB[IHostApplicationBuilder] -->|AddSafHost| SHB[ISafHostBuilder]
    SHB -->|uses| PSB[IPluginSystemHostBuilder]
    PSB -->|registers| SPI[ServicePluginHost\nIHostedService]
    SPI -->|calls| PM[IPluginManifest.ConfigureServices\nper plug-in]
    SPI -->|starts/stops| SP[IServicePlugin\nper plug-in]
```

## Minimal Setup

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddSafHost();

// Register at least one messaging infrastructure factory
builder.Services.AddInProcessMessagingInfrastructure();

// Register at least one storage infrastructure
builder.Services.AddLiteDbStorageInfrastructure(c => c.ConnectionString = "Filename=app.db");

var host = builder.Build();
await host.RunAsync();
```

`AddSafHost()` reads plugin system configuration from the `"PluginSystem"` section and service host info from the `"ServiceHost"` section of your configuration (e.g. `appsettings.json`). It also registers SAF's built-in plugin assemblies from the application base directory using an explicit allow-list.

At the moment the built-in list contains:

- `SAF.Hosting.dll`
- `SAF.Messaging.Runtime.dll`

This ensures that host-level SAF features continue to work even when those assemblies are referenced directly by `SAF.Hosting` instead of being added through an application-specific plugin scan.

---

## Configuration

### PluginSystem section

Controls how plug-ins are discovered and configured:

```json
{
  "PluginSystem": {
    "PluginSettingsRootPath": "./config",
    "PluginSettingsFilePath": "./pluginsettings.json",
    "PluginContractsSearchPattern": "MyApp.Contracts.dll"
  }
}
```

| Key | Default | Description |
|---|---|---|
| `PluginSettingsRootPath` | `./config` | Root directory for per-plugin `pluginsettings.json` files |
| `PluginSettingsFilePath` | `./pluginsettings.json` | Path relative to `PluginSettingsRootPath` for the plugin's settings file |
| `PluginContractsSearchPattern` | *(empty)* | Semicolon-separated glob patterns for assemblies exposing public plugin service types |

### ServiceHost section

Controls host identity and file system paths:

```json
{
  "ServiceHost": {
    "Id": "my-unique-node-id",
    "ServiceHostType": "MyApp",
    "FileSystemUserBasePath": "tempfs",
    "FileSystemInstallationPath": ".",
    "EnableDiagnostics": false
  }
}
```

| Key | Default | Description |
|---|---|---|
| `Id` | *(auto-generated GUID)* | Unique identifier of this host instance |
| `ServiceHostType` | `"SAF"` | Logical type label for the host |
| `FileSystemUserBasePath` | `"tempfs"` | Base directory for application-specific runtime data |
| `FileSystemInstallationPath` | `AppContext.BaseDirectory` | Installation root directory |
| `EnableDiagnostics` | `false` | Write diagnostic node-info to disk on startup |

### Messaging section

Required by `SAF.Messaging.Runtime` to select which messaging factory to expose as the primary `IMessagingInfrastructure`:

```json
{
  "Messaging": {
    "PrimaryKey": "InProcess"
  }
}
```

The value must match one of the well-known keys: `InProcess`, `Redis`, `Nats`, `Cde`, `Routing`.

---

## Programmatic Configuration

You can also configure the host entirely in code, without `appsettings.json`:

```csharp
builder.AddSafHost(pluginSystemOptions =>
{
    pluginSystemOptions.PluginSettingsRootPath = "./config";
    pluginSystemOptions.PluginContractsSearchPattern = "MyContracts.dll";
})
.ConfigureHostInfo(hostOptions =>
{
    hostOptions.Id = "node-1";
    hostOptions.ServiceHostType = "Demo";
    hostOptions.FileSystemUserBasePath = Path.Combine(AppContext.BaseDirectory, "data");
})
.ConfigurePluginSystem(ps =>
{
    ps.AddPluginAssemblyFolderContainer(options =>
    {
        options.SearchRootPath = AppContext.BaseDirectory;
        options.IncludePatterns = "MyApp.Plugin.*.dll";
        options.Recursive = false;
    });
})
.AddHostDiagnostics();
```

---

## Plugin Assembly Discovery

`AddSafHost()` already registers SAF's built-in plugin assemblies (`SAF.Hosting.dll` and `SAF.Messaging.Runtime.dll`) from `AppContext.BaseDirectory`.

Additional `AddPluginAssemblyFolderContainer` calls should therefore be used for application-specific or externally deployed plugins.

The `AddPluginAssemblyFolderContainer` call controls which DLL files are scanned for `IPluginManifest` implementations.

```csharp
ps.AddPluginAssemblyFolderContainer(options =>
{
    // Root directory to search in
    options.SearchRootPath = AppContext.BaseDirectory;

    // Whether to recurse into subdirectories
    options.Recursive = false;

    // Semicolon-separated file glob patterns to include
    options.IncludePatterns = "MyApp.Plugin.*.dll";

    // Semicolon-separated patterns to exclude
    options.ExcludePatterns = "Microsoft.*;System.*;SAF.PluginSystem.*";
});
```

You can call `AddPluginAssemblyFolderContainer` multiple times to add assemblies from different directories.

---

## Diagnostics

Enable diagnostics to write host info (version, paths, environment) to disk at startup:

```csharp
builder.AddSafHost().AddHostDiagnostics();
```

Or via configuration:

```json
{ "ServiceHost": { "EnableDiagnostics": true } }
```

---

## IServiceHostInfo

Every plug-in can inject `IServiceHostInfo` to read host identity and path information at runtime:

```csharp
public class MyPlugin(IServiceHostInfo hostInfo)
{
    public void DoWork()
    {
        var dataPath = hostInfo.FileSystemUserBasePath;
        var id       = hostInfo.Id;
    }
}
```

---

## DI Container Layout

```mermaid
graph TB
    subgraph "Main (Host) Container"
        direction TB
        MSI[IMessagingInfrastructure]
        SSI[IStorageInfrastructure]
        SHI[IServiceHostInfo]
        LOG[ILogger / ILoggerFactory]
        CFG[IConfiguration]
    end

    subgraph "Plugin A Container"
        MSI_A[IMessagingInfrastructure\n(same instance)]
        SSI_A[IStorageInfrastructure\n(same instance)]
        SHI_A[IServiceHostInfo\n(same instance)]
        PA_PRIV[PrivateServiceA\n(isolated)]
    end

    subgraph "Plugin B Container"
        MSI_B[IMessagingInfrastructure\n(same instance)]
        SSI_B[IStorageInfrastructure\n(same instance)]
        SHI_B[IServiceHostInfo\n(same instance)]
        PB_PRIV[PrivateServiceB\n(isolated)]
    end

    MSI -->|shared| MSI_A
    MSI -->|shared| MSI_B
    SSI -->|shared| SSI_A
    SSI -->|shared| SSI_B
    SHI -->|shared| SHI_A
    SHI -->|shared| SHI_B
```

Infrastructure services from the main container are forwarded into every plugin container. Private plugin services remain invisible to other plugins.

> For a full explanation of the DI model, see [Plugin System — DI Containers](./plugin-system.md#di-containers).

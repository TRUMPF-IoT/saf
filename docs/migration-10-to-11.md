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
| NATS server | Any version | **2.2 or newer**, because `CustomProperties` now travel in NATS headers |
| Binary payloads on Redis and NATS | Not available | Sent by default; 9.x and 10.x nodes read them as corrupt text, so set `EnableBinaryPayloads` to `false` while they share the server; see [Redis and NATS send binary payloads](#redis-and-nats-send-binary-payloads) |
| Received messages | A C-DEngine handler could change a batched message without affecting other subscriptions | Messages are read-only on every transport; see [Messages are read-only](#messages-are-read-only) |

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

Values the 10.x host set in the `AddCdeInfrastructure` callback, such as `ApplicationId` above, now go into the `Cde` section. If a value must not live in a file, for example an id compiled into the host or a secret, see [Values the host passed in code](#values-the-host-passed-in-code).

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
| `services.AddCdeInfrastructure(...)` | Load `SAF.Messaging.Cde.dll` **and** `SAF.Storage.Cde.dll`; `Messaging:PrimaryKey = "Cde"`; `Cde` section (see [below](#c-dengine-storage-is-a-plug-in-of-its-own)) |
| `services.AddCde(...)` + `AddCdeMessagingInfrastructure()`, storage from another backend | Load `SAF.Messaging.Cde.dll` and that backend's storage plug-in; `Messaging:PrimaryKey = "Cde"`; `Cde` section |
| `services.AddRedisInfrastructure(...)` | Load `SAF.Messaging.Redis.dll`; `Messaging:PrimaryKey = "Redis"`; `Redis` section (provides messaging **and** storage) |
| `services.AddLiteDbStorageInfrastructure(...)` | Load `SAF.Storage.LiteDb.dll`; `LiteDb` section |
| `services.AddSQLiteStorageInfrastructure(...)` | Load `SAF.Storage.SQLite.dll`; `SQLite` section |

See [Messaging Infrastructure](./messaging.md) and [Storage Infrastructure](./storage.md) for the exact configuration sections.

### C-DEngine storage is a plug-in of its own

`AddCdeInfrastructure` registered C-DEngine messaging **and** a C-DEngine storage. In 11.x these are two
plug-ins: `SAF.Messaging.Cde.dll` provides messaging only, and `SAF.Storage.Cde.dll` provides the storage.
Both read the `Cde` section and share one C-DEngine node per process. A host that relied on the C-DEngine
storage, for example for the host id that `SAF.Hosting` keeps there, loads both. The storage reads the
cache files that 10.x wrote, so nothing is lost. A host without any storage plug-in gets a new host id on
every start and logs a warning, see [ServiceHost section](./saf-host.md#servicehost-section).

If you load both, deploy them to the host's base directory, preferably through a `PackageReference` in the
host; a shared plug-in folder elsewhere is not enough. See [C-DEngine](./messaging.md#c-dengine) for why.

This also breaks hosts that already moved to `11.0.0-alpha.9` or an earlier 11.0 preview, where
`SAF.Messaging.Cde.dll` still registered the storage and `AddCdeInfrastructure` still existed. Without
`SAF.Storage.Cde.dll`, such a host has no C-DEngine storage any more.

### Values the host passed in code

In 10.x the host could fill the infrastructure's options in the `Add*Infrastructure(...)` callback, for example with a value compiled into the host or a secret it decrypted itself. In 11.x the plug-in makes that call and binds its options from its configuration section. The host therefore supplies such values through the plugin configuration.

Before (10.x):

```csharp
services.AddCdeInfrastructure(cdeConfig =>
{
    cdeConfig.ApplicationId = MyHostConstants.CdeApplicationId;
    cdeConfig.ScopeId = MyHostCrypto.Decrypt(configuration["Cde:EncryptedScopeId"]);
});
```

After (11.x):

```csharp
builder.AddSafHost()
    .ConfigurePluginSystem(ps =>
    {
        ps.AddPluginConfigurationSource(source =>
            source.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cde:ApplicationId"] = MyHostConstants.CdeApplicationId,
            }));
        ps.AddSecretConfigurationResolution(o => o.Namespace = "myapp");
    });
```

```json
{
  "Cde": {
    "ScopeId": "secret://cde/scope-id"
  }
}
```

Notes:

- **Use the property's own key.** The plug-in binds only the property names of its options class. It does not read a key that the 10.x host read and decrypted itself, such as `Cde:EncryptedScopeId`. Store the secret in the [secret store](./secret-store.md) and put the reference under the property's key (`Cde:ScopeId`).
- **Keep the section in the plugin settings file.** If a section exists in the plugin configuration, it hides the host's section of the same name entirely, and secret references are resolved only in the plugin configuration.
- **If the secret cannot move to the secret store yet,** a root decorator can decrypt it in the meantime.

There is no programmatic hook on a plug-in's options type, so the host does not reference the plug-in assembly. The details, including the decorator for host-decrypted values, are in [Setting plug-in values from the host](./plugin-system.md#setting-plug-in-values-from-the-host).

---

## Step 8 — Package Reference Changes

Update your `.csproj` files:

| Old Package | New Package |
|---|---|
| `SAF.Common` (for `IMessagingInfrastructure`) | `SAF.Messaging.Contracts` |
| `SAF.Hosting` (for `IServiceAssemblyManifest`) | `SAF.PluginSystem.Hosting.Contracts` (for `IPluginManifest`) |
| `SAF.Hosting` (for host bootstrap) | `SAF.Hosting` (unchanged, but API changed) |
| `IMessageHandler` plugin registration without extensions | Add package `SAF.Messaging.Extensions` and use `AddSingletonMessageHandler<T>()` / `AddTransientMessageHandler<T>()` + `AddMessageHandlerResolver()` |
| `SAF.Messaging.Cde` (for `CdeConfiguration`, `CdeCryptoLibConfig`, `AddCde`) | `SAF.Cde.Common`, namespace `SAF.Cde.Common` (see [below](#c-dengine-configuration-moved-to-safcdecommon)) |

### C-DEngine configuration moved to `SAF.Cde.Common`

`CdeConfiguration`, `CdeCryptoLibConfig` and `AddCde` moved from `SAF.Messaging.Cde` to the new package
`SAF.Cde.Common`, namespace `SAF.Cde.Common`. `SAF.Cde.Common` owns the one C-DEngine node of the process,
which the C-DEngine plug-ins share instead of each starting its own.

Code that names these types, for example a host that reads the `Cde` section into a `CdeConfiguration`,
replaces `using SAF.Messaging.Cde;` with `using SAF.Cde.Common;`. The package comes with `SAF.Messaging.Cde`;
add a `PackageReference` to `SAF.Cde.Common` only if you use the types without the plug-in package. The `Cde`
configuration section itself is unchanged.

This also breaks hosts that already moved to `11.0.0-alpha.9` or an earlier 11.0 preview, where these
types still lived in `SAF.Messaging.Cde`.

### Plugin assembly validation is a separate package

Plugin assembly validation is opt-in and lives in `SAF.PluginSystem.Hosting.Extensions`. `SAF.PluginSystem.Hosting` does not reference it, so the core engine does not pull the validators and their cryptography dependencies into applications that load plug-ins without validating them.

To use `AddStrongNamePluginAssemblyValidator`, `AddDigitalSignaturePluginAssemblyValidator` or your own `IPluginAssemblyValidator`, reference the package explicitly:

```xml
<PackageReference Include="SAF.PluginSystem.Hosting.Extensions" />
```

That package references `System.Security.Cryptography.Pkcs` for Authenticode CMS signature parsing, which it brings along transitively.

### `PluginAssemblyFolderContainer` constructor

Constructing this type directly — instead of using `AddPluginAssemblyFolderContainer` — takes seven parameters:

```csharp
new PluginAssemblyFolderContainer(
    loggerFactory, manifestLoader, options, fileSystem,
    assemblyValidators, sharedAssemblyResolver, sharedAssemblyConflictBehavior);
```

`assemblyValidators` takes a sequence of `IPluginAssemblyValidator` — an empty one keeps 10.x's behaviour of loading without validation. `sharedAssemblyResolver` is harder to supply yourself: its only implementation, `SharedAssemblyResolver`, is `internal` and is registered solely by `AddPluginSystem()`. In practice, constructing this container outside of `AddPluginAssemblyFolderContainer` means either resolving `ISharedAssemblyResolver` from a service provider that already had `AddPluginSystem()` applied to it, or implementing that (public) interface yourself. `sharedAssemblyConflictBehavior` is the `SharedAssemblyConflictBehavior` enum described under [Version handling](./plugin-system.md#version-handling).

### NATS messaging keeps blocking backpressure

`SAF.Messaging.Nats` now builds on **NATS.Net 3.x**, which stopped forcing
`SubPendingChannelFullMode = BoundedChannelFullMode.Wait` inside the `NatsClient` constructor; the
`NatsOpts` default is `DropNewest`. SAF sets `Wait` explicitly, so a subscription whose `IMessageHandler`
is slower than the publish rate still applies backpressure to the reader instead of silently discarding
messages — the 10.x behaviour. No action is required; the note is here because the underlying default
inverted, so a host that builds its own `NatsOpts` has to set the mode itself.

### NATS transports custom properties and requires NATS Server 2.2

SAF 10.x sent only topic and payload over NATS; `Message.CustomProperties` were lost on the way. SAF 11.x sends
them in the NATS headers `saf-v` and `saf-meta`, which NATS supports since server version **2.2**. The message
body is still the payload, and a message without custom properties carries no headers, so it looks exactly as
in 10.x.

**Required:** run NATS Server 2.2 or newer. An older server rejects every message with custom properties and
closes the publishing connection; the message is lost.

In mixed operation, 9.x and 10.x nodes keep working with 11.x nodes: they ignore the headers and receive topic
and payload, as before, but no custom properties. See [NATS](./messaging.md#nats) for the details.

### Redis drops messages of unknown wire format versions

A Redis receiver in 11.x reads the envelope versions `1.x` and `2.x` that all SAF versions since 9.x write. An
envelope with any other major version is now **dropped** with a warning, logged once per version, instead of
being read as version 2. No action is required: 9.x, 10.x and 11.x all write version `2.0.0`. See
[Redis](./messaging.md#redis).

### Messages are read-only

`Message` has new optional members: `BinaryPayload`, `AcceptedReplyFormats` and `GetFormat()` (see
[The Message Type](./messaging.md#the-message-type)). Existing code compiles and behaves as before.

A message must not be changed after it is published, neither by the publisher nor by a handler. That was already
necessary for In-Process messaging, which hands the published instance itself to all handlers. The C-DEngine
transport now does the same for batched messages: all subscriptions of a node get one instance, where 10.x gave
each subscription its own copy.

**Required:** check handlers that modify the `Message` they receive, for example by rewriting `Topic` or adding a
custom property before passing it on. Create a new `Message` instead. See
[Messages Are Read-Only](./messaging.md#messages-are-read-only).

### C-DEngine nodes announce pub/sub version 5.0.0

An 11.x node announces the C-DEngine pub/sub version `5.0.0`, which carries binary payloads (see
[C-DEngine](./messaging.md#c-dengine)). 9.x and 10.x nodes keep exchanging text messages with it as before.
A message with a `BinaryPayload` is not sent to them; the sender logs a warning per such peer. No action is
required, but a feature that sends binary payloads works only between 11.x nodes.

### Redis and NATS send binary payloads

SAF 11.x sends a message with a `BinaryPayload` over Redis and NATS without Base64: on Redis in a binary frame,
on NATS as the message body (see [Redis](./messaging.md#redis) and [NATS](./messaging.md#nats)). Text messages
look on the wire as in 10.x.

A 9.x or 10.x node cannot read such a message and cannot tell: it receives the bytes as the `Payload` of a
message, as corrupt text, and logs no error. Redis and NATS have no per-node negotiation like C-DEngine, so the
sender cannot leave older nodes out.

**Required in mixed operation:** while 9.x or 10.x nodes use the same Redis or NATS server, set
`EnableBinaryPayloads` to `false` in the `Redis` or `Nats` section of every 11.x node:

```json
{
  "Redis": { "ConnectionString": "localhost:6379", "EnableBinaryPayloads": false },
  "Nats": { "Url": "nats://localhost:4222", "EnableBinaryPayloads": false }
}
```

A message with a `BinaryPayload` is then not sent, and the sender logs an error; text messages are not affected.
Remove the setting once all nodes run 11.x. Without older nodes, no action is required. See
[Binary Payloads With Older Nodes on Redis or NATS](./messaging.md#binary-payloads-with-older-nodes-on-redis-or-nats).

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
- [ ] If you used `AddCdeInfrastructure`, load `SAF.Storage.Cde.dll` next to `SAF.Messaging.Cde.dll`, both from the host's base directory
- [ ] Move values your host set in `Add*Infrastructure(...)` callbacks into the backend's configuration section: host constants via `AddPluginConfigurationSource`, secrets as `secret://` references (see [Values the host passed in code](#values-the-host-passed-in-code))
- [ ] Reference `SAF.PluginSystem.Hosting.Extensions` explicitly if you use plugin assembly validation, and check the `RequireValidDigitalSignature = true` default against the signatures your plug-ins actually carry
- [ ] Forward any additional host service your plug-ins need with `AddHostServiceForwarder<T>()` — v10's single shared container needed no such step
- [ ] Replace `using SAF.Messaging.Cde;` with `using SAF.Cde.Common;` wherever you use `CdeConfiguration` or `CdeCryptoLibConfig`
- [ ] If you use NATS, run NATS Server 2.2 or newer
- [ ] If 9.x or 10.x nodes share a Redis or NATS server with 11.x nodes, set `EnableBinaryPayloads` to `false` on the 11.x nodes until all nodes run 11.x
- [ ] Make sure no handler changes a `Message` it receives; create a new one instead

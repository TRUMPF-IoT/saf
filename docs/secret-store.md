# Secret Store

SAF's secret store keeps **sensitive configuration values** — usernames, passwords, tokens, key
passphrases — out of your configuration files and in a secure, OS-level store instead. Plug-ins read
and write secrets through a single injected service, `ISecretStore`, without knowing where or how the
secrets are physically stored.

> **Status.** The store, provider selection, transparent `secret://` configuration resolution, the
> Windows Credential Manager provider and the cross-platform file-based provider are available today. A
> systemd-credentials provider is planned — see [Roadmap](#roadmap).

## Why

Storing credentials in configuration files (even obfuscated) means the secret travels with every copy
of the file — into source control, backups, support bundles and other machines. The secret store
moves the secret into an OS-managed vault that is bound to a single security principal on a single
machine, so a leaked configuration file no longer leaks the credential.

## Security model

A credential store does not promise "nobody can read the secret" — it promises "only a principal with
sufficient privilege can". Understanding that boundary is important:

- **Not protected:** code running **as the service account** can read the secret (the service must be
  able to recover it to use it), and administrators/root can reach it through the machine key. This is
  inherent and no application-level mechanism removes it.
- **The reference is not sensitive.** A secret *name* / reference may appear in configuration, logs
  and source control — never rely on it being secret.
- **What you gain over in-file encryption:** the secret is no longer in the file (no leak via copies,
  backups or source control); protection is per-machine and per-principal (hardware-backed where the
  OS supports it); and access can be audited.

The right operational posture is to run the process under a least-privileged identity and let that
identity own the secrets.

## Packages

| Package | Purpose |
|---|---|
| `SAF.Configuration.Secrets.Contracts` | Interfaces and types: `ISecretStore`, `ISecretReader`, `ISecretWriter`, `ISecretStoreProvider`, `ISecretProtector`, `SecretStoreOptions`, `FileSecretStoreOptions`, `SecretScope`, `SecretReference` |
| `SAF.Configuration.Secrets` | Provider implementations (Windows Credential Manager, file store), the default `PkcsSecretProtector`, and provider selection |
| `SAF.Configuration.Secrets.Extensions` | Plugin-system host-builder integration (`AddSecretStore`, `AddSecretConfigurationResolution`) |

Reference `SAF.Configuration.Secrets.Extensions` from your host; it pulls in the other two.

## Getting Started

### 1. Register the secret store on the host

`AddSecretStore` is an extension on the plugin system host builder. It registers the store with the
built-in providers for the current platform and **forwards `ISecretStore` into every plug-in
container**, so any plug-in can inject it.

```csharp
using SAF.Configuration.Secrets;

builder.AddSafHost()
    .ConfigurePluginSystem(ps =>
    {
        ps.AddPluginAssemblyFolderContainer(options =>
        {
            options.SearchRootPath = AppContext.BaseDirectory;
            options.IncludePatterns = "MyApp.Plugin.*.dll;SAF.Messaging.InProcess.dll";
            options.Recursive = false;
        });

        ps.AddSecretStore(options => options.Namespace = "myapp");
    });
```

### 2. The contracts assembly is shared automatically

`ISecretStore` is forwarded from the host into every plug-in container via `IHostServiceForwarder` (see
[Plugin System: IHostServiceForwarder](./plugin-system.md#ihostserviceforwarder)). For a plug-in to
accept the forwarded instance, its isolated load context must resolve `ISecretStore` to the *same*
`SAF.Configuration.Secrets.Contracts` assembly the host uses — the plugin system does this automatically
for any assembly it finds in the host's own base directory. Referencing
`SAF.Configuration.Secrets.Extensions` from your host project is normally all it takes: the contracts
assembly is a transitive dependency, so the build already places it next to your host executable.

> Do **not** add it to `PluginContractsSearchPattern` — that setting controls a different mechanism,
> discovering **cross-plugin service exports** (see
> [Cross-Plugin Services](./plugin-system.md#cross-plugin-services)), not host-to-plugin forwarding.
> Adding it here additionally registers `ISecretStore` as an exported cross-plugin service, which is not
> what you want.

### 3. Inject and use `ISecretStore` in a plug-in

```csharp
using SAF.Configuration.Secrets.Contracts;

public sealed class OpcUaConnection(ISecretStore secrets)
{
    public async Task ConnectAsync(CancellationToken ct)
    {
        var user = await secrets.GetSecretAsync("opcua/connection-1/user", ct);
        var password = await secrets.GetSecretAsync("opcua/connection-1/password", ct);

        if (user is null || password is null)
            throw new InvalidOperationException("OPC UA credentials are not provisioned.");

        // ... open the session with user / password ...
    }
}
```

The API is small:

```csharp
namespace SAF.Configuration.Secrets.Contracts;

public interface ISecretStore : ISecretReader, ISecretWriter;

public interface ISecretReader
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}

public interface ISecretWriter
{
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
    Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default);
}
```

`GetSecretAsync` returns `null` when the secret does not exist; `RemoveSecretAsync` succeeds even when
it does not.

## Providers

Each backend is an `ISecretStoreProvider`. More than one provider can be available on the same
platform (for example, Windows can offer both the Credential Manager and the file store); each
provider decides via `IsAvailable` whether it applies to the current environment.

| Provider | Name | Availability |
|---|---|---|
| Windows Credential Manager | `windows-credential-manager` | Windows only. Stores secrets as generic credentials in the running identity's vault (per-principal isolation). |
| File store | `file` | Cross-platform. Persists secrets to a single JSON file, each value encrypted at rest through an `ISecretProtector`. Reports itself **unavailable until a protector is registered** (see below). |

> On non-Windows platforms the file store is the built-in default, but it only becomes *available* once
> you register an `ISecretProtector` (there is no OS-integrated at-rest encryption to fall back on). See
> [The file store and its protector](#the-file-store-and-its-protector).

## Selecting providers

Two independent axes control which provider is used:

1. **Registration** — which providers are candidates (you choose, in priority order).
2. **Selection at runtime** — `SecretStoreOptions.ProviderName`:
   - `"auto"` (default) picks the **first available** provider in registration order.
   - a specific name (e.g. `"windows-credential-manager"`) forces that provider.

### Default registration

Omitting the provider callback registers all built-in providers for the platform in a documented
priority (OS-native store before the file store):

```csharp
ps.AddSecretStore(o => o.Namespace = "myapp"); // = AddDefaults()
```

- **Windows** — the Credential Manager (zero-config). The file store is *not* added by default; add it
  explicitly with `.AddFile()` if you want it.
- **Non-Windows** — the file store, as the platform default. It stays unavailable until you register an
  `ISecretProtector`; without one, `"auto"` selection fails with a clear *"no available secret store
  provider"* error rather than an obscure startup crash.

### Explicit registration

Pass a provider callback to register **exactly** the providers you want, in priority order. The order
of the calls is the priority used by `"auto"`:

```csharp
ps.AddSecretStore(
    configure: o => o.ProviderName = "auto",
    configureProviders: providers => providers
        .AddWindowsCredentialManager());
```

### Custom providers

Add your own backend (for example a remote key vault) without modifying the framework — implement
`ISecretStoreProvider` and register it:

```csharp
ps.AddSecretStore(null, providers => providers
    .AddProvider<MyKeyVaultProvider>()   // 1st priority
    .AddWindowsCredentialManager());     // fallback
```

## The file store and its protector

The `file` provider persists secrets to a single JSON file (default:
`<BaseDirectory>/secrets/<namespace>.secrets.json`, override with `FileSecretStoreOptions.Path`). The
**logical names stay in clear** — a secret reference is not itself sensitive — while **each value is
encrypted at rest** through an injected `ISecretProtector`.

The protector (and its key/certificate material) is *not* registered for you: it is a deployment
decision, so you register it explicitly. The built-in, cross-platform default is `PkcsSecretProtector`
(PKCS#7/CMS enveloping: AES-256 for the value, RSA-OAEP-SHA256 for the key, keyed by an X.509
certificate you supply):

```csharp
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.Protection;

// Register a protector, then the file store. On non-Windows AddDefaults() already registers the file
// store, so registering the protector alone is enough there.
ps.Services.AddSingleton<ISecretProtector>(_ => new PkcsSecretProtector(certificate));

ps.AddSecretStore(
    configure: o => o.Namespace = "myapp",
    configureProviders: providers => providers.AddFile(o => o.Path = "/var/lib/myapp/secrets.json"));
```

Key points:

- **Unavailable without a protector.** If no `ISecretProtector` is registered, the file store reports
  `IsAvailable = false` (and logs a warning explaining how to register one). `"auto"` then skips it;
  forcing it by name yields a clear *"not available"* error. It never fails to construct — so a Windows
  host that uses `AddDefaults().AddFile()` without a protector still works via the Credential Manager.
- **Protector identity is stamped** into the file. Opening a store written by a different protector
  fails fast with an explanatory error rather than producing garbage.
- **File permissions are the installer's responsibility.** The provider only reads and writes the file
  contents; it does **not** set ACLs (`0600` on Linux, an NTFS ACL for the reader on Windows). Lock the
  file down at deployment time so only the service account can read it.

> **Windows alternative (planned).** A DPAPI-backed protector can be added additively for Windows-only
> file stores without changing the store — see [Roadmap](#roadmap).

## Transparent configuration resolution

Besides injecting `ISecretStore` directly, SAF can resolve secrets **transparently in configuration**:
put a reference instead of the value in your plugin configuration, and existing
`IConfiguration`/`Bind`-based plug-ins receive the real secret with no code change.

Enable it on the host builder (compose it with `AddSecretStore`, or use it on its own):

```csharp
ps.AddSecretConfigurationResolution(o => o.Namespace = "myapp");
```

Then reference secrets in the plugin configuration with the `secret://` prefix:

```json
{
  "OpcUaConnections": [
    {
      "User": "secret://myapp/opcua/conn-1/user",
      "Password": "secret://myapp/opcua/conn-1/password",
      "Host": "opc.tcp://plc-1:4840"
    }
  ]
}
```

- Values **with** the prefix are replaced by the resolved secret; values **without** it (e.g. `Host`)
  pass through unchanged.
- An **environment variable** derived from the reference name overrides the store, which lets
  CI/containers inject secrets without an OS store. The name is `EnvironmentVariablePrefix` plus the
  reference name with `/` → `__` and other non-alphanumeric characters → `_`; e.g.
  `secret://myapp/opcua/conn-1/password` → `SECRET__myapp__opcua__conn_1__password`.
- Provider selection/registration is the same as `AddSecretStore` (default = platform providers, or
  pass `configureProviders` to choose explicitly). `AddSecretConfigurationResolution` and
  `AddSecretStore` compose safely, so transparent resolution and direct `ISecretStore` injection can be
  used together.

> **Resolution before DI exists.** Configuration is built before the application's DI container.
> Resolution therefore starts with a self-contained reader and automatically switches to the host's DI
> `ISecretStore` once the container is available (re-resolving if a value changed). This is transparent
> — no action required.

## Options

`SecretStoreOptions` (configure via the `AddSecretStore` / `AddSecretConfigurationResolution` callback):

| Option | Default | Meaning |
|---|---|---|
| `ProviderName` | `"auto"` | Which provider is active. `"auto"` = first available in registration order; or a provider name to force it. |
| `Scope` | `ServiceAccount` | Isolation scope (see below). |
| `Namespace` | `"saf"` | Prepended to every secret name to form the store key, so different products/hosts do not collide. |
| `ReferencePrefix` | `"secret://"` | Marks a configuration value as a secret reference (transparent resolution). |
| `AllowEnvironmentOverride` | `true` | When resolving a reference, check a derived environment variable before the store. |
| `EnvironmentVariablePrefix` | `"SECRET"` | Prefix of that environment variable. |

### Scope

`Scope` is the **isolation axis** — who may read the secret — not the identity itself. The running
identity is arbitrary (`LocalSystem`, `NetworkService`, a virtual `NT SERVICE\*` account, a gMSA, or a
local/domain user).

- `ServiceAccount` (default) — bound to a single principal. For the Windows Credential Manager this is
  inherent: the secret lives in the running identity's vault.
- `Machine` — any local account may read. The Windows Credential Manager has no machine-wide vault, so
  it logs a warning and still stores per-principal; broader readership for the file provider is a matter
  of the file's deployed permissions (see [The file store and its protector](#the-file-store-and-its-protector)).

> `SecretStoreOptions` also exposes `RequireSecretReferences`, intended to fail-fast on plaintext in a
> secret-backed field. The transparent resolver does not enforce it yet — it resolves references and
> passes all other values through unchanged.

## Secret names

A secret name is a logical key such as `opcua/connection-1/password`. The active `Namespace` is
prepended to form the physical store key (e.g. `myapp/opcua/connection-1/password`). Names are not
secret and may be committed to configuration and source control.

## Provisioning secrets

Secrets must exist in the store before the service reads them. Use `ISecretStore.SetSecretAsync` from
your own tooling/installer, or provision them out-of-band (for the Windows Credential Manager, under
the identity the service runs as). SAF intentionally contains **no migration logic** — moving existing
in-file secrets into the store is the responsibility of each product.

## Roadmap

The following are planned and not yet available:

- **systemd-credentials provider** for Linux services — the intended zero-config, OS-native Linux
  default, registered ahead of the file store in `AddDefaults` once it ships.
- **DPAPI-backed `ISecretProtector`** for Windows-only file stores, added additively alongside the
  default `PkcsSecretProtector`.
- **Enforcing `RequireSecretReferences`** — fail-fast when a secret-backed configuration field holds a
  plaintext value instead of a reference.

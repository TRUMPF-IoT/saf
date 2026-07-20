# Secret Store

SAF's secret store keeps **sensitive configuration values** — usernames, passwords, tokens, key
passphrases — out of your configuration files and in a secure, OS-level store instead. Plug-ins read
and write secrets through a single injected service, `ISecretStore`, without knowing where or how the
secrets are physically stored.

> **Status.** The store, provider selection and the Windows Credential Manager provider are available
> today. A file-based provider, a systemd-credentials provider, and transparent `secret://`
> configuration resolution are planned — see [Roadmap](#roadmap).

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
| `SAF.Configuration.Secrets.Contracts` | Interfaces and types: `ISecretStore`, `ISecretReader`, `ISecretWriter`, `ISecretStoreProvider`, `SecretStoreOptions`, `SecretScope`, `SecretReference` |
| `SAF.Configuration.Secrets` | Provider implementations and provider selection |
| `SAF.Configuration.Secrets.Extensions` | Plugin-system host-builder integration (`AddSecretStore`) |

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

### 2. Share the contracts assembly across the plug-in boundary

`ISecretStore` lives in its own contracts assembly, which the plug-in system must recognise as a
**shared contract** so plug-ins resolve the same type the host forwards. Add it to
`PluginContractsSearchPattern` (SAF's own contracts are added automatically; this one is not):

```json
{
  "PluginSystem": {
    "PluginContractsSearchPattern": "SAF.Configuration.Secrets.Contracts.dll"
  }
}
```

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
platform (for example, Windows can offer both the Credential Manager and — once shipped — a file
store); each provider decides via `IsAvailable` whether it applies to the current environment.

| Provider | Name | Availability |
|---|---|---|
| Windows Credential Manager | `windows-credential-manager` | Windows only. Stores secrets as generic credentials in the running identity's vault (per-principal isolation). |

> Today the only built-in provider is the Windows Credential Manager. On non-Windows platforms there
> is currently no built-in provider — register a custom one via `AddProvider<T>` (see below) until the
> file/systemd providers ship.

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

## Options

`SecretStoreOptions` (configure via the `AddSecretStore` callback):

| Option | Default | Meaning |
|---|---|---|
| `ProviderName` | `"auto"` | Which provider is active. `"auto"` = first available in registration order; or a provider name to force it. |
| `Scope` | `ServiceAccount` | Isolation scope (see below). |
| `Namespace` | `"saf"` | Prepended to every secret name to form the store key, so different products/hosts do not collide. |

### Scope

`Scope` is the **isolation axis** — who may read the secret — not the identity itself. The running
identity is arbitrary (`LocalSystem`, `NetworkService`, a virtual `NT SERVICE\*` account, a gMSA, or a
local/domain user).

- `ServiceAccount` (default) — bound to a single principal. For the Windows Credential Manager this is
  inherent: the secret lives in the running identity's vault.
- `Machine` — any local account may read. The Windows Credential Manager has no machine-wide vault, so
  it logs a warning and still stores per-principal; this scope targets the upcoming file provider.

> `SecretStoreOptions` also exposes `ReferencePrefix`, `RequireSecretReferences`,
> `AllowEnvironmentOverride` and `EnvironmentVariablePrefix`. These belong to the planned transparent
> configuration resolution (see [Roadmap](#roadmap)) and have no effect yet.

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

- **File-based provider** (cross-platform), with an NTFS ACL / POSIX ownership so an installer can
  write a store that only the service account can read.
- **systemd-credentials provider** for Linux services.
- **Transparent configuration resolution** — reference a secret from configuration as
  `"secret://myapp/opcua/connection-1/password"` and have SAF resolve it automatically, so existing
  `IConfiguration`/`Bind`-based plug-ins pick up the real value with no code change. The
  `ReferencePrefix`, `RequireSecretReferences`, `AllowEnvironmentOverride` and
  `EnvironmentVariablePrefix` options and the `SecretReference` helper support this upcoming feature.

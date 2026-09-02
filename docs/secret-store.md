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
- **Resolved secrets are plaintext in the process's own configuration state.** Once a `secret://`
  reference resolves, the value lives in `IConfiguration` like any other setting, so
  `IConfigurationRoot.GetDebugView()` prints it, attributed to the resolving provider. Use the
  `GetDebugView(Func<string, ConfigurationDebugViewContext, string> processValue)` overload to mask
  values before logging or displaying a debug view of configuration that may include resolved secrets.
- **The environment is a trust boundary only if you make it one.** With `AllowEnvironmentOverride`
  enabled, anyone able to set this process's environment — a systemd drop-in, a container `-e` flag, a
  sibling process that can spawn the service — can substitute **any** secret without touching the
  keystore or the encrypted file. That is why it defaults to `false`; see
  [Environment overrides](#environment-overrides).
- **The writer is trusted.** Provisioning (whoever calls `SetSecretAsync` — an installer, an admin
  tool) is assumed authorized, not adversarial. In particular, `PkcsSecretProtector`'s PKCS#7/CMS
  envelope encrypts each value (AES-256-CBC content key wrapped with RSA-OAEP) but does not
  authenticate it: `Protect` only needs the recipient certificate's *public* key, so anyone who can
  write the store file could swap, replay, or forge entries without needing the private key. This is
  an accepted trade-off given the trusted-writer assumption, not an oversight — if your deployment
  cannot guarantee that, do not rely on this store's write path as a tamper-resistant boundary.

The right operational posture is to run the process under a least-privileged identity and let that
identity own the secrets.

## Packages

| Package | Purpose |
|---|---|
| `SAF.Configuration.Secrets.Contracts` | Interfaces and types: `ISecretStore`, `ISecretReader`, `ISecretWriter`, `ISecretStoreProvider`, `ISecretProtector`, `SecretStoreOptions`, `FileSecretStoreOptions`, `SecretReference` |
| `SAF.Configuration.Secrets` | Provider implementations (Windows Credential Manager, file store), the default `PkcsSecretProtector`, and provider selection |
| `SAF.Configuration.Secrets.Extensions` | Plugin-system host-builder integration (`AddSecretStore`, `AddSecretConfigurationResolution`) |

Reference `SAF.Configuration.Secrets.Extensions` from your host; it pulls in the other two.

## Getting Started

### 1. Register the secret store on the host

`AddSecretStore` is an extension on the plugin system host builder. It registers the store with the
built-in providers for the current platform and **forwards `ISecretStore` into every plug-in
container**, so any plug-in can inject it.

```csharp
using SAF.Configuration.Secrets.Extensions;

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

> **One provider answers, and it is not a fallback chain.** Selection happens once; the chosen provider
> serves every read *and* every write for the process lifetime. A name it does not hold is absent —
> the next registered provider is never consulted for it. That keeps provisioning and resolution on the
> same backend, and keeps "which store answered?" a question with one answer. Registration order
> therefore decides *which* provider is used here, not what is tried after a miss.
>
> Only a *failed selection* is retried: if no provider was available on the first attempt, the next call
> selects again, so a provider whose availability is a runtime fact (a remote vault) can recover without
> a restart.

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
    .AddProvider<MyKeyVaultProvider>()   // used when available
    .AddWindowsCredentialManager());     // used only if the vault provider is unavailable
```

## The file store and its protector

The `file` provider persists secrets to a single JSON file. Unless `FileSecretStoreOptions.Path` is set,
it defaults to a per-machine data location — `%ProgramData%\<namespace>\secrets.json` on Windows,
`/var/lib/<namespace>/secrets.json` elsewhere — not the host application directory: per
[Plugin Deployment Security](./plugin-security.md), that directory must be read-only to the runtime
account, which rules it out as a location this provider writes to. The **logical names stay in clear**
— a secret reference is not itself sensitive — while **each value is encrypted at rest** through an
injected `ISecretProtector`.

The protector (and its key/certificate material) is *not* registered for you: it is a deployment
decision, so you register it explicitly. The built-in, cross-platform default is `PkcsSecretProtector`
(PKCS#7/CMS enveloping: AES-256 for the value, RSA-OAEP-SHA256 for the key, keyed by an X.509
certificate you supply):

```csharp
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.Extensions;
using SAF.Configuration.Secrets.Protection;

// Register a protector, then the file store. On non-Windows AddDefaults() already registers the file
// store, so registering the protector alone is enough there.
ps.Services.AddSingleton<ISecretProtector>(_ => new PkcsSecretProtector(certificate));

ps.AddSecretStore(
    configure: o => o.Namespace = "myapp",
    configureProviders: providers => providers.AddFile(o => o.Path = "/var/lib/myapp/secrets.json"));
```

`certificate` needs its private key on the service host (`Unprotect` requires it) but only its public
key on a provisioning/installer host (`Protect` works either way). How you obtain it matters:

- **Prefer an already-installed certificate**, looked up via `X509Store` +
  `X509Certificate2Collection.Find(X509FindType.FindByThumbprint, ...)` — the OS owns the key material
  and its lifecycle, so there is nothing for this library to leak or clean up.
- **If you must load a PFX directly**, avoid `X509KeyStorageFlags.EphemeralKeySet`: on Windows, CMS
  decryption needs a CNG key handle, which an ephemeral key does not provide. The alternative —
  `X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.Exportable)` without
  `EphemeralKeySet` — persists the private key into the CNG key store
  (`%APPDATA%\Microsoft\Crypto\Keys` under the running identity's profile) as a side effect of loading,
  accumulating one key file per load. Combine `MachineKeySet` with `PersistKeySet` and lock down the
  resulting key file's ACL to the service account only, or accept the per-load key accumulation only in
  short-lived provisioning tools that run once.
- **`PkcsSecretProtector` takes ownership of `certificate`** — it implements `IDisposable` and disposes
  the certificate (releasing its private-key handle) when disposed. Registering it via
  `AddSingleton<ISecretProtector>` as shown is enough: the DI container disposes it with the rest of the
  host. Do not also dispose `certificate` yourself once it is handed to the protector.

Key points:

- **Unavailable without a protector.** If no `ISecretProtector` is registered, the file store reports
  `IsAvailable = false` (and logs a warning explaining how to register one). `"auto"` then skips it;
  forcing it by name yields a clear *"not available"* error. It never fails to construct — so a Windows
  host that uses `AddDefaults().AddFile()` without a protector still works via the Credential Manager.
- **A public-key-only certificate fails clearly on `Unprotect`.** A certificate without its private key
  is enough to `Protect` (e.g. from a provisioning host that should only ever write, never read), but
  calling `Unprotect` with it throws `InvalidOperationException` naming the problem, instead of an
  opaque `CryptographicException` from deep inside CMS decryption.
- **Protector identity is stamped** into the file. Opening a store written by a different protector
  fails fast with an explanatory error rather than producing garbage. This guards against
  misconfiguration (wrong protector wired up), not against tampering — per the trusted-writer
  assumption above, the stamp itself is unauthenticated and a missing stamp is not rejected.
- **File permissions are the installer's responsibility.** The provider does **not** grant a specific
  principal access — lock the file down at deployment time so only the identity the service runs as can
  read it. Concretely, for a store at the default location and a service running as `NT SERVICE\MyApp`
  (Windows) or `myapp` (Linux):

  ```powershell
  icacls "$env:ProgramData\myapp\secrets.json" /inheritance:r /grant "NT SERVICE\MyApp:(R)" /grant "BUILTIN\Administrators:(F)"
  ```

  ```bash
  chown myapp:myapp /var/lib/myapp/secrets.json && chmod 600 /var/lib/myapp/secrets.json
  ```

  Every write happens **in place**, through a single handle on the store file itself, so an update never
  touches (and never widens) the file's existing permissions; a first write on Linux defaults to
  owner-only (`0600`) rather than the process umask. No temporary or sidecar file is ever created — the
  store also works on deployment targets that only permit writing an already-existing file. The
  document is serialized to memory first and written in a single call, so a serialization failure or a
  cancelled write leaves the previous content intact. The remaining trade-off: a process crash or a full
  disk *during* that one write can still truncate the file, since there is no atomic replace to fall back
  to. A store file damaged that way is reported at startup with an error naming the file — and, for an
  undecryptable value, the secret — rather than surfacing as an unrelated parse or format error.
- **Concurrent readers/writers are serialized**, including across processes (e.g. this host and a
  separate installer/CLI tool touching the same file): a write opens the store file exclusively for its
  duration, so a second reader or writer — in this process or another — waits its turn instead of racing.
  Reads share the file with other readers, so two hosts pointed at the same store resolve their
  configuration concurrently.

  That wait is **bounded by `FileSecretStoreOptions.LockTimeout`** (default 30 seconds), after which the
  operation throws a `TimeoutException` naming the file. A foreign holder of the file — a backup agent,
  an on-access virus scanner, an admin's editor — would otherwise block every secret operation in the
  process for as long as it kept the handle. Set `Timeout.InfiniteTimeSpan` to wait indefinitely instead.
  A failure that cannot clear by waiting is never retried: a denied ACL or an unreachable path is
  reported immediately, and a store file that does not exist simply has no secrets.

- **The parsed file is cached between reads**, and revalidated against the file's last-write time and
  size on every lookup. Resolving configuration asks for each reference in turn, so re-reading and
  re-parsing the whole file per lookup made startup quadratic in the number of references. The store
  drops its cache whenever it writes, and a change made by another process is picked up through the
  write stamp. Only the encrypted document is cached — never a decrypted value, which is still
  unwrapped per lookup.

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
      "User": "secret://opcua/conn-1/user",
      "Password": "secret://opcua/conn-1/password",
      "Host": "opc.tcp://plc-1:4840"
    }
  ]
}
```

> **A reference does not repeat the namespace.** It carries the *logical* name only; the store prepends
> the configured `Namespace` to form the physical key (see [Secret names](#secret-names)). With
> `o.Namespace = "myapp"`, `secret://opcua/conn-1/password` is looked up as
> `myapp/opcua/conn-1/password`. Writing `secret://myapp/opcua/conn-1/password` instead would look up
> `myapp/myapp/opcua/conn-1/password` and fail to resolve.

- Values **with** the prefix are replaced by the resolved secret; values **without** it (e.g. `Host`)
  pass through unchanged.
- A reference that **no provider can resolve throws by default** (`ThrowOnUnresolvedReference`), naming
  the reference and the configured provider, instead of silently becoming `null` and shadowing the
  original value. Set it to `false` for test/dev scenarios without a populated store.
- An **environment variable** can override the store, so CI/containers can inject secrets without an
  OS store. This is **off by default** — see [Environment overrides](#environment-overrides), which
  also covers why it is off and the one naming pitfall to know about before turning it on.
- Provider selection/registration is the same as `AddSecretStore` (default = platform providers, or
  pass `configureProviders` to choose explicitly). `AddSecretConfigurationResolution` and
  `AddSecretStore` compose safely, so transparent resolution and direct `ISecretStore` injection can be
  used together.

> **Registration order does not matter, and a reference is never handed out unresolved.** Every plugin
> configuration source is resolved, whether its `AddPluginConfigurationSource` callback ran before or
> after `AddSecretConfigurationResolution`. Resolution reads the *composed* plugin configuration once
> every source has been built, and the resolved values are layered on top of it — so a settings file
> registered by a later callback is resolved like any other, and each file is still parsed and watched
> exactly once.
>
> The standalone `IConfigurationBuilder.AddResolvedSecrets(...)` has no such control over when the root
> is built. It resolves against the builder's sources as they stand at `Build()` time, which costs a
> second materialization of those sources, and it cannot override a source added *after* it — those
> providers answer first. A reference coming from such a source therefore **throws at startup**, naming
> the configuration keys, rather than passing the literal `secret://…` token to the consumer as its
> credential. Call `AddResolvedSecrets` last to avoid it.
>
> That guarantee needs a builder that defers building until `Build()`. `ConfigurationManager` — the
> builder behind `Host.CreateApplicationBuilder().Configuration` and
> `WebApplication.CreateBuilder().Configuration` — instead builds every source as soon as it is added, so
> the resolver could see neither the later sources nor the fact that they shadow it. `AddResolvedSecrets`
> therefore **refuses a `ConfigurationManager` with a `NotSupportedException`** instead of failing open.
> Compose and resolve in a `ConfigurationBuilder`, then chain the built root in:
>
> ```csharp
> var resolved = new ConfigurationBuilder()
>     .AddJsonFile("appsettings.json")
>     .AddResolvedSecrets(o => o.Namespace = "myapp")
>     .Build();
>
> builder.Configuration.AddConfiguration(resolved);
> ```
>
> For plug-in configuration, prefer `AddSecretConfigurationResolution` — it resolves against the composed
> root and has none of these ordering constraints.

> **Enabling resolution does not change unrelated settings.** A value that is not a `secret://`
> reference is served exactly as the undecorated configuration would serve it, empty strings included: a
> `"Suffix": ""` an operator configured deliberately still reads as `""`, not `null`. Only values that
> parse as a reference are replaced.

> **How resolution reaches the host container.** Plugin configuration is built inside the same factory
> that constructs `IPluginSystemHostContext`, which the plugin system only ever invokes once the host's
> `IServiceProvider` is fully built (see [Plugin System: Plugin
> Settings](./plugin-system.md#plugin-settings)). The resolver receives that provider and reads the
> `ISecretStore` and `SecretStoreOptions` registered on it directly — there is no separate bootstrap
> phase, and no action is required to make that happen.

## Options

`SecretStoreOptions` (configure via the `AddSecretStore` / `AddSecretConfigurationResolution` callback):

| Option | Default | Meaning |
|---|---|---|
| `ProviderName` | `"auto"` | Which provider is active. `"auto"` = first available in registration order; or a provider name to force it. |
| `Namespace` | `"saf"` | Prepended to every secret name to form the store key, so different products/hosts do not collide. |
| `ReferencePrefix` | `"secret://"` | Marks a configuration value as a secret reference (transparent resolution). |
| `AllowEnvironmentOverride` | `false` | When resolving a reference, check a derived environment variable before the store. See [Environment overrides](#environment-overrides). |
| `EnvironmentVariablePrefix` | `"SECRET"` | Prefix of that environment variable. |
| `ThrowOnUnresolvedReference` | `true` | Throw when a `secret://` reference cannot be resolved, instead of passing it through as `null`. |
| `ResolveTimeout` | `30s` | Upper bound on resolving all references of one configuration load. Configuration loads synchronously, so without it an unreachable store hangs startup with no diagnostic. `Timeout.InfiniteTimeSpan` waits forever. Enforced by bounding the wait itself, so it also applies to a provider that blocks in a synchronous call and never observes cancellation. |

`FileSecretStoreOptions` (configure via `providers.AddFile(o => ...)`, or `services.AddFileSecretStore(o => ...)`):

| Option | Default | Meaning |
|---|---|---|
| `Path` | per-machine data location | Filesystem path of the store file. See [The file store and its protector](#the-file-store-and-its-protector). |
| `LockTimeout` | `30s` | Upper bound on waiting for another process to release its exclusive hold on the store file, after which the operation throws a `TimeoutException`. `Timeout.InfiniteTimeSpan` waits forever. |

## Secret names

A secret name is a logical key such as `opcua/connection-1/password`. The active `Namespace` is
prepended to form the physical store key (e.g. `myapp/opcua/connection-1/password`). Names are not
secret and may be committed to configuration and source control.

The physical store key is **case-insensitive** — `Namespace` and the name are both lower-cased before
use, the same way on every provider. This matches the Windows Credential Manager, which treats target
names case-insensitively regardless of what is written; without normalizing, the same logical secret
could resolve differently depending on which backend is active.

The file store also matches keys case-insensitively when it *reads*, so a `secrets.json` provisioned by
hand or by an installer resolves whatever case its keys were written in, and a later write updates that
entry instead of adding a second one beside it.

## Environment overrides

Setting `AllowEnvironmentOverride = true` makes resolution check an environment variable *before* the
store, so CI and development can supply secrets on a host with no OS keystore and no provisioned file:

```csharp
ps.AddSecretConfigurationResolution(o =>
{
    o.Namespace = "myapp";
    o.AllowEnvironmentOverride = true; // opt in — off by default
});
```

The variable name is `EnvironmentVariablePrefix`, then `__`, then the **physical store key** (so the
`Namespace` is included, exactly as in [Secret names](#secret-names)), with `/` → `__` and every other
non-alphanumeric character → `_`. The key is lower-cased first, so the derived name is too:

| `Namespace` | reference | variable |
|---|---|---|
| `myapp` | `secret://opcua/conn-1/password` | `SECRET__myapp__opcua__conn_1__password` |
| `other` | `secret://opcua/conn-1/password` | `SECRET__other__opcua__conn_1__password` |

Because the namespace is part of the name, two hosts sharing one environment but using different
namespaces do **not** share an override variable. On Linux, where environment lookup is
case-sensitive, note the lower-casing: `secret://Db/Password` reads `SECRET__myapp__db__password`.

> **The derivation is readable, not injective.** `/` becomes `__` while every other non-alphanumeric
> character becomes a single `_`, so two names can map onto one variable: under namespace `myapp`,
> both `a/b` and `a--b` yield `SECRET__myapp__a__b`, and both `a-b` and `a.b` yield
> `SECRET__myapp__a_b`. Setting that variable overrides *every* name mapping to it. Readable names
> were preferred over injective ones because an operator types these by hand; the risk only becomes
> real if two of your secret names differ solely in their non-alphanumeric characters, so avoid that
> pairing, or leave overrides off.

Because the override is checked *before* the store, it wins over a correctly provisioned keystore
entry unconditionally, and the value it supplies never passes through the store's at-rest protection.
That is what makes enabling it a widening of the trust boundary to everyone who can set the process
environment, and why it is off by default. When an override is applied the resolver logs it at
`Debug` with the reference and the variable name (never the value).

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

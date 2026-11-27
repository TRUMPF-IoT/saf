# Secret Store

SAF's secret store keeps **sensitive configuration values** — usernames, passwords, tokens, key
passphrases — out of your configuration files and in a secure, OS-level store instead. Plug-ins read
and write secrets through a single injected service, `ISecretStore`, without knowing where or how the
secrets are physically stored.

> **Status.** The store, provider selection, transparent `secret://` configuration resolution, the
> Windows Credential Manager provider and the cross-platform file-based provider, optionally with
> [signed values](#signed-values), are available today. A systemd-credentials provider is planned — see
> [Roadmap](#roadmap).

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
- **Whoever can write the configuration decides which key receives a secret.** Resolution replaces a
  reference wherever it appears. If a party you trust less than the secret — a settings page, an API,
  another node — can write the plugin configuration, it can put a reference into a key whose value
  leaves the machine, such as a host name or a URL, and the plug-in sends the secret there. Register a
  policy that binds each secret to its keys; see [Restricting references](#restricting-references).
- **The writer is trusted — unless values are signed.** Provisioning (whoever calls `SetSecretAsync` —
  an installer, an admin tool) is assumed authorized, not adversarial. In particular,
  `PkcsSecretProtector`'s PKCS#7/CMS envelope encrypts each value (AES-256-CBC content key wrapped with
  RSA-OAEP) but does not authenticate it: `Protect` only needs the recipient certificate's *public*
  key, so anyone who can write the store file could swap, replay, or forge entries without needing the
  private key. If your deployment cannot guarantee that only authorized writers reach the file, wrap
  the protector in `SigningSecretProtector` (see [Signed values](#signed-values)): writing a value then
  needs the certificate's private key, and a forged, changed or swapped value is refused when it is
  read. Deleting an entry and replaying a value of the same name stay possible either way: an older one,
  or one from another store signed with the same certificate.

The right operational posture is to run the process under a least-privileged identity and let that
identity own the secrets.

## Packages

| Package | Purpose |
|---|---|
| `SAF.Configuration.Secrets.Contracts` | Interfaces and types: `ISecretStore`, `ISecretReader`, `ISecretWriter`, `ISecretStoreProvider`, `ISecretProtector`, `ISecretReferencePolicy`, `SecretStoreOptions`, `FileSecretStoreOptions`, `SecretReference` |
| `SAF.Configuration.Secrets` | Provider implementations (Windows Credential Manager, file store), the default `PkcsSecretProtector`, the `SigningSecretProtector` decorator, the reference policies (`KeyBoundSecretReferencePolicy`, `SecretReferenceAllowList`, `AnySecretReferencePolicy`), and provider selection |
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
            options.IncludePatterns = "SAF.Messaging.InProcess.dll;MyApp.Plugin.*.dll";
            options.Recursive = false;
        });

        ps.AddSecretStore(options => options.Namespace = "myapp");
    });
```

### 2. The contracts assembly is shared automatically

`ISecretStore` is forwarded from the host into every plug-in container via `IHostServiceForwarder` (see
[Plugin System: IHostServiceForwarder](./plugin-system.md#ihostserviceforwarder)). For a plug-in to
accept the forwarded instance, its isolated load context must resolve `ISecretStore` to the *same*
`SAF.Configuration.Secrets.Contracts` assembly the host uses. `AddSecretStore` handles this: it registers
the forwarder through `AddHostServiceForwarder<ISecretStore>()`, which adds the contracts assembly to the
plugin system's [shared set](./plugin-system.md#the-shared-set) in the same step. Nothing to configure.

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
> a restart. Configuration resolution treats obtaining the store the same way: when that fails during a
> reload, the previously resolved values are kept and the next reload tries again, instead of latching
> the failure for the process lifetime.

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

A provider receives the **physical target name** — `Namespace` already prepended, the whole key
lower-cased — not the logical name the reference carried. The convention is applied once, by the
composite store, before it delegates: a custom provider inherits it without knowing about it, and must
store the name it is given rather than applying `Namespace` a second time. For the same reason, resolve
and call `ISecretStore`; an `ISecretStoreProvider` resolved directly would be handed logical names it is
not meant to interpret.

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
  misconfiguration (wrong protector wired up), not against tampering — the stamp itself is
  unauthenticated and a missing stamp is not rejected. With [signed values](#signed-values), changing
  the stamp gains nothing: every value is still verified when it is read.
- **File permissions are the installer's responsibility — including the directory.** The provider
  neither grants nor restricts any principal's access: it writes where it is pointed, accepting whatever
  ACL that location already carries. Restricting the store is therefore a **required install step**, and
  it has to cover the *directory*, before the service first runs. The default Windows location makes that
  concrete: `C:\ProgramData` grants `BUILTIN\Users` read (inherited by everything below it) plus
  add-file/add-subdirectory, so a store directory left to be created by the first write is
  **readable by every local user** — and any local user can **pre-create** `%ProgramData%\<namespace>\`
  beforehand, own it as `CREATOR OWNER` with full control, and grant themselves what they like; the
  provider takes the directory as it finds it. Create it in the installer instead, break inheritance, and
  grant only the service identity and administrators. For a service running as `NT SERVICE\MyApp`
  (Windows) or `myapp` (Linux):

  ```powershell
  $store = "$env:ProgramData\myapp"
  New-Item -ItemType Directory -Force -Path $store | Out-Null
  # (RX) is enough when the installer provisions the secrets; use (M) if the service writes them itself.
  icacls $store /inheritance:r /grant "NT SERVICE\MyApp:(OI)(CI)(RX)" /grant "BUILTIN\Administrators:(OI)(CI)(F)"
  ```

  ```bash
  install -d -o myapp -g myapp -m 700 /var/lib/myapp
  ```

  The store file inherits that ACL when the provider creates it, so no separate `icacls` on
  `secrets.json` is needed; on Linux a first write additionally sets the file itself to `0600`. Audit the
  directory, not only the file: a locked-down file in a directory somebody else can write to is not
  protected.

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

  A write stamp is trusted only once it is **older than the coarsest filesystem timestamp granularity**
  (2 s; ~16 ms on NTFS, 1 s on ext3, 2 s on FAT/exFAT). Inside that window a second write can land in the
  same timestamp bucket, and rotating a credential to a same-length value leaves the size unchanged as
  well — so the stamp would compare equal to a document that is already stale. Reads within ~2 s of a
  write therefore re-read the file instead of caching it, and only after that does the stamp become
  evidence that the cached document is current.

### Signed values

`PkcsSecretProtector` alone trusts whoever can write the store file, because protecting a value needs
only the public key. `SigningSecretProtector` wraps another protector and signs each value it produces,
so that writing a value needs the certificate's **private** key. Use it when the file's ACL is not the
only thing you want between a local user and a forged secret, for example because an installer, a
backup tool or a support engineer may write the file:

```csharp
// The same certificate encrypts (PkcsSecretProtector) and signs (SigningSecretProtector).
ps.Services.AddSingleton<ISecretProtector>(_ =>
    new SigningSecretProtector(new PkcsSecretProtector(certificate), certificate));
```

The certificate needs an RSA key. If it has a key usage extension, that must include `digitalSignature`
for signing and `keyEncipherment` for `PkcsSecretProtector`. To keep the keys for encryption and
signing apart, pass a second certificate:
`new SigningSecretProtector(new PkcsSecretProtector(encryptionCertificate), signingCertificate)`. One
certificate for both works as well: a value is verified before it is decrypted, so nobody without the
private key can have data of their choice decrypted with it.

- **What is signed.** The value as the inner protector produced it, together with the secret's
  physical name (see [Secret names](#secret-names)), in a PKCS#7/CMS `SignedData` with RSA-PSS and
  SHA-256. The certificate is not embedded.
- **What a read refuses**, with a `CryptographicException` naming the secret:
  - a value that is not signed, such as one written by `PkcsSecretProtector` alone;
  - a value signed by another key, including a signature that names the configured certificate but
    carries a certificate of its own;
  - a value signed for another name, such as one moved to another entry of the file;
  - a value changed after it was signed;
  - a value with more than one signature.
- **What it does not detect.**
  - A deleted entry, which then counts as missing.
  - An older value of the same name that was valid when it was written (rollback). Only state kept
    outside the file, such as a counter, could detect that.
  - A value of the same name copied from another store file signed with the same certificate, for
    example when several hosts share one certificate. The signature binds the name, not the file or the
    host. Give each store its own certificate to rule that out.
- **Keys.** Signing needs the private key, so a host that only provisions secrets needs it too, unlike
  with `PkcsSecretProtector` alone; without it, `Protect` throws an `InvalidOperationException` naming
  the problem. A certificate whose key usage lacks `digitalSignature` is refused by the constructor, and
  a key the operating system allows only to decrypt makes `Protect` throw a `CryptographicException`
  naming the certificate. Verifying needs only the public key, and decrypting needs whatever the inner
  protector needs.
- **No chain and no validity period.** A value is verified against the configured certificate and
  nothing else: no chain is built and its dates are not checked. A self-signed certificate works, and so
  does an expired one, so a store certificate that expires never makes the secrets unreadable. Trust
  comes from the certificate being the configured one, so protect its private key: whoever can use it
  can set any secret.
- **Names** are compared case-insensitively, like the [store keys](#secret-names). A name that contains
  NUL or an unpaired surrogate cannot be signed unchanged, so `Protect` refuses it with an
  `ArgumentException`.
- **Protector name.** It is the inner protector's name with `-signed` appended, `pkcs-signed` for the
  example above. A file written by `PkcsSecretProtector` alone is stamped `pkcs`, and the store refuses
  every read **and write** on a file stamped by another protector. Switching an existing store therefore
  needs a new file; see [Switching an existing store](#switching-an-existing-store).
- **Ownership.** `SigningSecretProtector` disposes the inner protector (if it is `IDisposable`) and the
  certificate when it is disposed itself. Passing the same certificate instance to both, as above, is
  fine.

#### Switching an existing store

There is no in-place upgrade, and the store cannot list its secrets, so the product must know their
names. Stop the service first, and start it again only once it is configured with the new protector:
a value it writes during the switch would be lost, and with the old protector it refuses the new file.
Then read the secrets with the old protector, write them to a new file with the new one, and only then
put the new file in place of the old one:

```csharp
// A stand-alone store over one file, for example in an installer.
static ServiceProvider BuildStore(string path, ISecretProtector protector)
{
    var services = new ServiceCollection().AddLogging();
    services.AddSingleton<ISecretProtector>(_ => protector);
    services.AddSecretStore(o => o.Namespace = "myapp").AddFile(o => o.Path = path);
    return services.BuildServiceProvider();
}

string[] names = ["db/password", "mqtt/password"]; // every secret the product stores
var values = new Dictionary<string, string>();

await using (var current = BuildStore(path, new PkcsSecretProtector(LoadCertificate())))
{
    var store = current.GetRequiredService<ISecretStore>();
    foreach (var name in names)
    {
        if (await store.GetSecretAsync(name) is { } value)
        {
            values[name] = value;
        }
    }
}

// Created empty and owner-only before the store opens it. CreateNew refuses a file left over from an
// earlier attempt, which may still hold secrets removed from the store since.
var newPath = path + ".new";
var staging = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
if (!OperatingSystem.IsWindows())
{
    staging.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}
File.Open(newPath, staging).Dispose();

var certificate = LoadCertificate();
await using (var signed = BuildStore(newPath, new SigningSecretProtector(new PkcsSecretProtector(certificate), certificate)))
{
    var store = signed.GetRequiredService<ISecretStore>();
    foreach (var (name, value) in values)
    {
        await store.SetSecretAsync(name, value);
    }
}

File.Move(newPath, path, overwrite: true);
```

`LoadCertificate()` stands for however you obtain the certificate; each protector disposes the instance
it is given.

The new file is created empty before anything is written to it, so the switch also works when none of
the secrets exist: the new store is then empty. If an earlier attempt left `secrets.json.new` behind,
`File.Open` throws an `IOException` and nothing is changed. Delete that file and run the migration
again. It is not reused because the store keeps entries that are not written again, so it could bring
back secrets that were removed in the meantime.

Unlike the store's own writes, which change the file in place, this creates a second file next to the
store, so the account that runs it must be allowed to create files in that directory. The new file
also does not keep the old file's permissions:

- **Windows:** it inherits the ACL of the directory. If the directory is restricted as described under
  *File permissions* above, the old file had that ACL as well; entries set on the old file itself are
  lost.
- **Linux:** it is created with mode `0600` and is owned by the account that runs the migration. Run the
  migration as the service account (for example with `sudo -u myapp`), or give the file the old owner
  and mode before the service starts again (for example
  `chown myapp:myapp /var/lib/myapp/secrets.json`). Otherwise the service can no longer read its
  secrets.

Provisioning every secret again works the same way, once the old file has been moved away or deleted;
the same applies to the new file's permissions.

### Writing a protector

Both methods of `ISecretProtector` receive the secret's physical name. A protector that does not need
it, like `PkcsSecretProtector`, ignores it. A protector that wraps another one passes it on, so that a
protector further in, such as `SigningSecretProtector`, can bind the value to it.

> **Windows alternative (planned).** A DPAPI-backed protector can be added additively for Windows-only
> file stores without changing the store — see [Roadmap](#roadmap). It can be wrapped in
> `SigningSecretProtector` like the default one.

## Transparent configuration resolution

Besides injecting `ISecretStore` directly, SAF can resolve secrets **transparently in configuration**:
put a reference instead of the value in your plugin configuration, and existing
`IConfiguration`/`Bind`-based plug-ins receive the real secret with no code change.

> **New plug-ins read their secrets from `ISecretStore`.** A plug-in that asks the store for its secret by
> name (step 3 of [Getting Started](#getting-started)) keeps no reference in the configuration. On a
> host without transparent resolution, nobody who can write the configuration can point that secret at
> another key, and the host needs only `AddSecretStore`. Once the host also enables transparent
> resolution, for example for older plug-ins, any key may refer to that secret too, unless a
> [policy](#restricting-references) refuses it: with `KeyBoundSecretReferencePolicy` or
> `SecretReferenceAllowList`, give secrets that are only read through `ISecretStore` names that the policy
> allows at no key. Transparent resolution is for plug-ins that already bind their settings from
> configuration.

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
  `AddSecretStore` compose, so transparent resolution and direct `ISecretStore` injection can be used
  together — subject to one rule: **register the providers in exactly one of the two calls.**

> **Composing the two calls.** Both register the same store, so the order of the two calls does not
> matter and calling either twice changes nothing. Options callbacks from both are applied in call order, so a
> property set in both takes the value from the later call.
>
> The provider list is the exception. The order providers are registered in is the priority order
> `ProviderName = "auto"` selects from, and registration *appends*: a second list would land behind the
> first rather than replace it, leaving the store reading from a backend nobody chose. So pass
> `configureProviders` to one call only — the other takes what is already registered:
>
> ```csharp
> ps.AddSecretStore(o => o.Namespace = "myapp", p => p.AddFile());
> ps.AddSecretConfigurationResolution();   // same store, same options, same providers
> ```
>
> Passing it to both throws an `InvalidOperationException` during registration, naming both calls, instead
> of letting the call order decide the active backend. A call that omits it accepts whatever is already
> registered — including providers registered directly, e.g. `services.AddSecretStore().AddFile()` — and
> registers the platform defaults only when nothing is.

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

### Restricting references

By default every key may refer to every secret. When parties that must not read the secrets can write
the plugin configuration — a settings page, an API, another node — register a policy that binds each
secret to its keys. SAF has two:

- **`KeyBoundSecretReferencePolicy`** lets a key refer only to the secret whose name is spelled from it:
  `OpcUaConnections:0:Password` to `opcuaconnections/0/password`, lowercased and with `:` as `/`. Nothing has to be
  listed, so a plug-in can add a secret without a change to the host, as long as it names the secret
  after its key. A key that contains `/` may refer to no secret, since `a/b` and `a:b` would share one
  name. Nor may a key with a character that lower-cases to a letter configuration keys still tell apart
  from it, such as the Kelvin sign `K` (U+212A, lower-cased `k`): `Kiosk` with that sign and `Kiosk`
  with a plain `K` are two keys but would share one name. Letters such as `Ä` or `ß` are not affected.
- **`SecretReferenceAllowList`** lists the keys each secret may appear in, for names that do not follow
  the key: a secret shared by several keys, or one in an array whose order may change.

`AnySecretReferencePolicy` combines them, allowing what any of them allows:

```csharp
ps.Services.AddSecretReferencePolicy(new AnySecretReferencePolicy([
    new KeyBoundSecretReferencePolicy(),
    new SecretReferenceAllowList([
        new("OpcUaConnections:0:User", "opcua/conn-1/user"),
        new("OpcUaConnections:0:Password", "opcua/conn-1/password"),
    ]),
]));
ps.AddSecretConfigurationResolution(o => o.Namespace = "myapp");
```

- A reference the policy refuses **reads as `null`** and is logged as a warning naming the key and the
  reference; the store is not read for it. Startup does not fail over it, so a writer cannot stop the
  host this way. The one exception is the standalone `AddResolvedSecrets(...)`: a refused reference from
  a source added after it throws at startup like any other reference from such a source, because that
  source would serve the literal `secret://…` token instead of `null`.
- The policy is asked on every load and every reload, so a reference written while the host runs is
  refused as soon as the configuration reloads.
- Both policies match keys case-insensitively, as configuration keys match, and names the way the store
  looks them up (see [Secret names](#secret-names)). Implement `ISecretReferencePolicy` for other rules.
- A container takes one policy: a second `AddSecretReferencePolicy` throws instead of replacing the
  first and lifting its restrictions. Combine several with `AnySecretReferencePolicy`.
- With the standalone `AddResolvedSecrets(...)`, pass the policy as its `policy` argument:
  `.AddResolvedSecrets(o => o.Namespace = "myapp", policy: policy)`. Asking it then does not run
  `configureProviders`, which runs only once the store is read. A policy registered inside
  `configureProviders` is not used there, and the load throws an `InvalidOperationException` when it
  finds one. The standalone overload logs nothing unless it gets a `loggerFactory` argument, such as the
  host's `ILoggerFactory`. Pass one so refusals are logged: `.AddResolvedSecrets(o => o.Namespace =
  "myapp", policy: policy, loggerFactory: loggerFactory)`. The store uses the same factory.

> **Keep the policy out of the writers' reach.** Build it in code, not from configuration those parties
> can change, or they extend it. The policy binds a secret to its keys, not to where the plug-in sends
> it: a writer who can change `OpcUaConnections:0:Host` still redirects the password. Keep the keys that
> decide where a secret goes out of their reach as well.

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
use, once, for every provider alike. This matches the Windows Credential Manager, which treats target
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
> `SECRET__myapp__a_b`. Readable names were preferred over injective ones because an operator types
> these by hand.
>
> When one configuration load refers to two or more names that share a variable, none of them is read
> from it: each is read from the store, and a warning names the references and the variable. This
> matters with a [policy](#restricting-references) such as `KeyBoundSecretReferencePolicy`, where whoever
> writes the configuration picks the names by picking the keys: a writer who adds `Conn_1` next to
> `Conn-1` gets the store's value for `conn_1`, not the variable set for `conn-1`. It does not help
> when the name the variable was set for is not referenced in the same load, so do not leave override
> variables set for names nothing refers to.

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

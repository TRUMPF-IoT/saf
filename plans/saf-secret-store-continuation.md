# SAF Secret Store — Handoff zur Fortsetzung (nur SAF-Repo)

> Kompakte Übergabe, um die **SAF-seitigen Erweiterungen** des Secret Store in einer neuen Session
> fortzuführen. Bewusst auf das SAF-Repo beschränkt — die QDS-Migration ist ein separater Strang und
> hier ausgeklammert.

**Repo:** `d:\Repos\GitHub\TRUMPF-IoT\saf` · **Branch:** `feature/secret-store` (setzt auf
`feature/assembly-validation` auf) · bereits als NuGet **11.0.0-alpha.4** veröffentlicht.

## Was fertig & committet ist

Neue Pakete unter `src/Configuration/` (Dreiteilung Contracts → Impl → Extensions; im `.slnx` als
Ordner `/Configuration/`):

- **`SAF.Configuration.Secrets.Contracts`** (ns `SAF.Configuration.Secrets.Contracts`):
  - `ISecretReader` (`GetSecretAsync`), `ISecretWriter` (`SetSecretAsync`/`RemoveSecretAsync`, Rückgabe `Task`),
    `ISecretStore : ISecretReader, ISecretWriter`, `ISecretStoreProvider : ISecretStore` (+ `string Name`, `bool IsAvailable`).
  - `SecretScope { ServiceAccount, Machine }`.
  - `SecretStoreOptions`: `ProviderName="auto"` (const `AutoProviderName`), `Scope`, `ReferencePrefix="secret://"`,
    `Namespace="saf"`, `RequireSecretReferences=true`, `AllowEnvironmentOverride=true`, `EnvironmentVariablePrefix="SECRET"`.
  - `FileSecretStoreOptions` (`Path`, `ReaderPrincipal`) — **existiert bereits**, standalone/parallel zu den globalen Options.
  - `SecretReference` (`IsReference`/`TryParse`/`Parse`/`Build`/`ToString`, Props `Prefix`/`Name`). **Der Namespace steckt
    NICHT in der Referenz** — der Provider setzt ihn beim Zugriff via `BuildTargetName` (`{Namespace}/{name}`) davor.
- **`SAF.Configuration.Secrets`** (Impl):
  - `WindowsCredentialManager/`: `INativeCredentialApi` (intern) + `WindowsCredentialManagerNativeApi`
    (`[SupportedOSPlatform("windows")]`, `[ExcludeFromCodeCoverage]`, advapi32 CredRead/Write/Delete/Free, löscht
    Secret-Bytes nach Nutzung) + `WindowsCredentialManagerSecretStore` (`ProviderName="windows-credential-manager"`,
    `IsAvailable=OperatingSystem.IsWindows()`, warnt bei `Scope=Machine`).
  - `CompositeSecretStore` (intern): `auto` = erster verfügbarer Provider in Registrierungsreihenfolge, oder per Name;
    Lazy-Selection; klare `InvalidOperationException`, wenn keiner verfügbar. **Kein `IsAvailable` auf `ISecretStore` selbst.**
  - DI: `SecretStoreServiceCollectionExtensions` (`AddSecretStore`→`ISecretStoreBuilder`, `AddWindowsCredentialManagerSecretStore`),
    `ISecretStoreBuilder`/`SecretStoreBuilder`/`SecretStoreBuilderExtensions` (`AddWindowsCredentialManager`,
    `AddProvider<T>` via `TryAddEnumerable`, `AddDefaults`).
  - `Configuration/` (transparente Auflösung): `SecretResolvingConfigurationProvider`/`-Source`, `HostSecretStoreAccessor`
    (+ `HostSecretStoreAccessorInitializer` IHostedService), `SecretConfigurationBuilderExtensions.AddResolvedSecrets`.
    Zwei-Phasen-Vorwärts-Ansatz: vor DI self-contained Bootstrap-Reader, ab DI Umschalten auf Host-`ISecretStore`.
- **`SAF.Configuration.Secrets.Extensions`**: `PluginSystemHostBuilderExtensions` → `AddSecretStore(IPluginSystemHostBuilder, …)`
  (+ `HostServiceForwarder<ISecretStore>`) und `AddSecretConfigurationResolution(…)`.
- Tests: `SAF.Configuration.Secrets.Tests` + `SAF.Configuration.Secrets.Extensions.Tests` (xUnit.v3 + NSubstitute + Testably.Abstractions).
- `docs/secret-store.md` (Roadmap nennt die offenen Punkte).

**Commit-Historie (feature/secret-store):** `58b5105` docs transparent resolution → `394f8bb` transparent resolution →
`67efe02` docs → `5c8b012` provider selection + host integration → `9370b96` abstraction + Windows provider.

## Offen — die SAF-Erweiterungen für die neue Session

Alle bewusst „ganz zum Schluss" zurückgestellt:

1. **File-Provider (cross-platform)** — `ISecretStoreProvider`, `ProviderName="file"`, über `IFileSystem`
   (System.IO.Abstractions); Rechte **0600 (Linux)** bzw. **NTFS-ACL für einen konfigurierbaren Principal (Windows)**
   → installer-schreibbar, service-lesbar. Nutzt `FileSecretStoreOptions` (`Path`, `ReaderPrincipal`). Bildet
   `SecretScope.Machine` ab. **Höchste Priorität** (entblockt die QDS-Linux-Umstellung).
2. **systemd-credentials-Provider** — Linux, read-only, liest `$CREDENTIALS_DIRECTORY`.
3. **`RequireSecretReferences` erzwingen** — Fail-fast, wenn ein Secret-Feld Klartext statt Referenz enthält
   (im transparenten Resolver; aktuell nicht durchgesetzt).

**Design-Entscheidungen für den File-Provider:**
- ~~At-Rest-Verschlüsselung: DPAPI/`ProtectedData` (Windows-gebunden) vs. `System.Security.Cryptography.Pkcs` (plattformneutral).~~
  **ENTSCHIEDEN (2026-07-21): plattformneutrales `System.Security.Cryptography.Pkcs` (CMS) als Default**,
  weil der File-Provider plattformneutral sein soll. **Aber:** Die Verschlüsselung wird hinter eine
  eigene Abstraktion gelegt (z. B. `ISecretProtector`/`ISecretEncryptor` mit Protect/Unprotect über
  Byte-Arrays), damit die Architektur offen bleibt, später **additiv (OCP)** einen **DPAPI-basierten
  File-Provider für Windows** (`[SupportedOSPlatform("windows")]`, `System.Security.Cryptography.ProtectedData`)
  bereitzustellen. Default-Impl `PkcsSecretProtector` (cross-platform); der `FileSecretStore` nimmt den
  Protector als Dependency (DI), wählt ihn nicht selbst. Muster analog zu `ISecretStoreProvider` +
  `AddProvider<T>` / Windows-Native-Subsystem.
- ~~ACL/Permissions setzt der Provider beim Schreiben vs. reine Installer-Verantwortung.~~
  **ENTSCHIEDEN (2026-07-21): Reine Installer-Verantwortung.** Der File-Provider setzt keine
  Dateiberechtigungen — 0600 (Linux) bzw. NTFS-ACL (Windows) sind Aufgabe des Installers/Deployments.
  Der Provider liest/schreibt nur den Dateiinhalt. `FileSecretStoreOptions.ReaderPrincipal` bleibt damit
  ggf. reine Doku/Metadaten und wird vom Provider nicht zum aktiven Setzen von ACLs verwendet.

## Muster & Standards (einhalten)

- **net10.0**, **SPDX-MPL-2.0-Header in JEDER Quelldatei**, Central Package Management (`Directory.Packages.props`),
  MinVer, SourceLink, REUSE.
- **SOLID** + **Testabdeckung ≥80 %** (coverlet). Neue Provider über OCP: `ISecretStoreProvider` + `AddProvider<T>` →
  greifen automatisch im `CompositeSecretStore`.
- Cross-Platform-Native-Muster (wie Windows-Provider / Authenticode-Subsystem): Interface + `[SupportedOSPlatform]`-Impl +
  `OperatingSystem.Is…()`-Guards + P/Invoke hinter Interface (testbar), `[ExcludeFromCodeCoverage]` für Interop.
- Provider-Registrierung idempotent (`TryAddEnumerable`); `AddSecretStore` + `AddSecretConfigurationResolution` kombinierbar.
- Sicherheitsmodell: Referenzname ist **nicht** geheim; Code als Dienstkonto/Admin/root kann lesen (inhärent);
  Secret-Bytes nach Nativzugriff löschen. **Keine Migrationslogik in SAF** (Produktverantwortung).
- Nach Fertigstellung: neue Alpha veröffentlichen (Konsumenten wie QDS sind Windows-first; Linux wartet auf den File-Provider).

## Referenzen

- Ursprünglicher Plan: `C:\Users\brachmaier\.claude\plans\warm-zooming-tome.md`
- Memory: `saf-secret-store-plan.md` (Stand/Design-Entscheidungen), `saf-code-standards.md` (SOLID + ≥80 %).

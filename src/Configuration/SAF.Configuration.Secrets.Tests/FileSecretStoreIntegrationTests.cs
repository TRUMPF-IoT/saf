// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.FileStore;
using Testably.Abstractions;
using Xunit;

/// <summary>
/// Verifies <see cref="FileSecretStore"/>'s cross-process locking against the real, on-disk file
/// system. <see cref="Testably.Abstractions.Testing.MockFileSystem"/> models FileShare.None
/// conflicts faithfully within one process, but two independent <see cref="FileSecretStore"/>
/// instances (as used here) share no in-process state at all, so this is the closest equivalent to
/// two separate processes writing to the same store file without needing to spawn one.
/// </summary>
public sealed class FileSecretStoreIntegrationTests : IDisposable
{
    private const int WritesPerInstance = 10;

    private readonly string _directory = Directory.CreateTempSubdirectory("saf-secret-store-tests-").FullName;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrentInstances_DoNotLoseOrCorruptWrites()
    {
        var storePath = Path.Combine(_directory, "saf.secrets.json");
        var protector = new ReversingSecretProtector();
        var storeA = CreateStore(storePath, protector);
        var storeB = CreateStore(storePath, protector);

        var writesA = Enumerable.Range(0, WritesPerInstance)
            .Select(i => storeA.SetSecretAsync($"a/key-{i}", $"value-{i}", TestToken));
        var writesB = Enumerable.Range(0, WritesPerInstance)
            .Select(i => storeB.SetSecretAsync($"b/key-{i}", $"value-{i}", TestToken));

        await Task.WhenAll(writesA.Concat(writesB));

        // Every key was written exactly once under concurrent, independent instances contending on the
        // same file. A lost update or file corruption from inadequate cross-process locking would show
        // up here as a missing key or a deserialization failure.
        var reader = CreateStore(storePath, protector);
        for (var i = 0; i < WritesPerInstance; i++)
        {
            Assert.Equal($"value-{i}", await reader.GetSecretAsync($"a/key-{i}", TestToken));
            Assert.Equal($"value-{i}", await reader.GetSecretAsync($"b/key-{i}", TestToken));
        }
    }

    [Fact]
    public async Task SetSecretAsync_UsesRestrictiveUnixFileMode_ForNewFile()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only apply on non-Windows platforms.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var storePath = Path.Combine(_directory, "saf.secrets.json");
        var store = CreateStore(storePath, new ReversingSecretProtector());

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(storePath));
    }

    private static FileSecretStore CreateStore(string path, ISecretProtector protector) => new(
        new RealFileSystem(),
        Options.Create(new SecretStoreOptions()),
        Options.Create(new FileSecretStoreOptions { Path = path }),
        NullLogger<FileSecretStore>.Instance,
        protector);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp-only directory in %TEMP% is harmless.
        }
    }
}

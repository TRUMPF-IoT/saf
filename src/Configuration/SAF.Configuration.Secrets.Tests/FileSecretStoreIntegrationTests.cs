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
/// Verifies <see cref="FileSecretStore"/>'s write path against the real, on-disk file system.
/// <see cref="Testably.Abstractions.Testing.MockFileSystem"/> does not validate destination-side
/// failures (e.g. a directory occupying the store's path) the way the real OS does on either platform -
/// confirmed empirically against real NTFS and ext4 - so this one scenario needs real I/O to be
/// trustworthy.
/// </summary>
public sealed class FileSecretStoreIntegrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("saf-secret-store-tests-").FullName;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SetSecretAsync_RemovesTempFile_WhenAtomicReplaceFails()
    {
        var storePath = Path.Combine(_directory, "saf.secrets.json");
        // A directory occupying the store's own path makes the final Move/Replace step fail on both
        // Windows (UnauthorizedAccessException) and Linux (IOException).
        Directory.CreateDirectory(storePath);
        var store = new FileSecretStore(
            new RealFileSystem(),
            Options.Create(new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = storePath }),
            NullLogger<FileSecretStore>.Instance,
            new ReversingSecretProtector());

        var exception = await Record.ExceptionAsync(
            async () => await store.SetSecretAsync("conn/pw", "value", TestToken));

        Assert.NotNull(exception); // exact type is OS-dependent: UnauthorizedAccessException (Windows) vs. IOException (Linux)
        Assert.DoesNotContain(
            Directory.GetFiles(_directory),
            f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

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

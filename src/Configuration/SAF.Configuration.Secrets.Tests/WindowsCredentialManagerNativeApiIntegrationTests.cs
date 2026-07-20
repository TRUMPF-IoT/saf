// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.ComponentModel;
using SAF.Configuration.Secrets.WindowsCredentialManager;
using Xunit;

/// <summary>
/// Verifies the real advapi32-backed <see cref="WindowsCredentialManagerNativeApi"/> against the
/// live Windows Credential Manager. Skipped on non-Windows platforms. Uses a unique target name and
/// cleans up after itself so it does not disturb real credentials.
/// </summary>
public class WindowsCredentialManagerNativeApiIntegrationTests
{
    [Fact]
    public void ReadWriteDelete_RoundTripsAgainstRealCredentialManager()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The Windows Credential Manager is only available on Windows.");
            return;
        }

        var api = new WindowsCredentialManagerNativeApi();
        var target = $"saf-tests/{Guid.NewGuid():N}";
        try
        {
            Assert.False(api.TryReadGenericCredential(target, out _));

            api.WriteGenericCredential(target, "s3cr3t-äöü");
            Assert.True(api.TryReadGenericCredential(target, out var secret));
            Assert.Equal("s3cr3t-äöü", secret);

            Assert.True(api.DeleteGenericCredential(target));
            Assert.False(api.TryReadGenericCredential(target, out _));
            Assert.False(api.DeleteGenericCredential(target)); // idempotent
        }
        finally
        {
            try
            {
                api.DeleteGenericCredential(target);
            }
            catch (Win32Exception)
            {
                // Best-effort cleanup; the credential was already removed on the happy path.
            }
        }
    }
}

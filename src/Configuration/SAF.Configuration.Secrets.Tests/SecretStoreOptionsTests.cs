// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretStoreOptionsTests
{
    [Fact]
    public void Defaults_MatchTheDocumentedContract()
    {
        var options = new SecretStoreOptions();

        Assert.Equal("auto", options.ProviderName);
        Assert.Equal(SecretStoreOptions.AutoProviderName, options.ProviderName);
        Assert.Equal(SecretScope.ServiceAccount, options.Scope);
        Assert.Equal("secret://", options.ReferencePrefix);
        Assert.Equal("saf", options.Namespace);
        Assert.True(options.RequireSecretReferences);
        Assert.True(options.AllowEnvironmentOverride);
        Assert.Equal("SECRET", options.EnvironmentVariablePrefix);
    }

    [Fact]
    public void FileOptions_DefaultToUnset()
    {
        var options = new FileSecretStoreOptions();

        Assert.Null(options.Path);
        Assert.Null(options.ReaderPrincipal);
    }
}

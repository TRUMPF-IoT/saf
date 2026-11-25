// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Configuration;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class KeyBoundSecretReferencePolicyTests
{
    private readonly KeyBoundSecretReferencePolicy _policy = new();

    [Theory]
    [InlineData("MyPlugin:Password", "myplugin/password")]
    [InlineData("MyPlugin:Connections:0:Password", "myplugin/connections/0/password")]
    [InlineData("Password", "password")]
    public void Allows_TheNameSpelledFromTheKey(string key, string name)
        => Assert.True(_policy.Allows(key, Reference(name)));

    [Fact]
    public void Allows_TheNameWhateverTheCaseOfKeyAndName()
        => Assert.True(_policy.Allows("MYPLUGIN:password", Reference("MyPlugin/Password")));

    [Theory]
    [InlineData("MyPlugin:Password", "myplugin/user")]
    [InlineData("MyPlugin:Host", "myplugin/password")]
    [InlineData("MyPlugin:Password", "myplugin/password/old")]
    [InlineData("MyPlugin:Password", "otherplugin/password")]
    public void Refuses_EveryOtherName(string key, string name)
        => Assert.False(_policy.Allows(key, Reference(name)));

    [Fact]
    public void Refuses_EveryName_ForAKeyThatContainsTheNameDelimiter()
        => Assert.False(_policy.Allows("MyPlugin/Password", Reference("myplugin/password")));

    [Fact]
    public void Allows_Throws_OnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => _policy.Allows(null!, Reference("myplugin/password")));
        Assert.Throws<ArgumentNullException>(() => _policy.Allows("MyPlugin:Password", null!));
    }

    private static SecretReference Reference(string name) => SecretReference.Parse("secret://" + name, "secret://");
}

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

    // Each of these lower-cases to an ASCII letter (k, å, ω, ß), but configuration keys still tell them apart
    // from that letter, so the key would share the secret of a sibling key.
    [Theory]
    [InlineData("MyPlugin:Connections:\u212Aiosk:Password", "myplugin/connections/kiosk/password")]
    [InlineData("MyPlugin:\u212B:Password", "myplugin/\u00E5/password")]
    [InlineData("MyPlugin:\u2126:Password", "myplugin/\u03C9/password")]
    [InlineData("MyPlugin:\u1E9E:Password", "myplugin/\u00DF/password")]
    public void Refuses_AKeyWhoseLowerCaseIsAnotherKey(string key, string name)
        => Assert.False(_policy.Allows(key, Reference(name)));

    [Fact]
    public void Allows_AKeyWithLettersThatCaseFoldAsKeysDo()
        => Assert.True(_policy.Allows("Gr\u00F6\u00DFe:\u00C4", Reference("gr\u00F6\u00DFe/\u00E4")));

    [Fact]
    public void Allows_Throws_OnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => _policy.Allows(null!, Reference("myplugin/password")));
        Assert.Throws<ArgumentNullException>(() => _policy.Allows("MyPlugin:Password", null!));
    }

    private static SecretReference Reference(string name) => SecretReference.Parse("secret://" + name, "secret://");
}

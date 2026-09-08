// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretReferenceTests
{
    private const string Prefix = "secret://";

    [Theory]
    [InlineData("secret://myproduct/conn-1/password", true)]
    [InlineData("secret://x", true)]
    [InlineData("plain-value", false)]
    [InlineData("secret://", false)] // prefix only, no name
    [InlineData(null, false)]
    public void IsReference_ReflectsWhetherValueIsAReference(string? value, bool expected)
    {
        Assert.Equal(expected, SecretReference.IsReference(value, Prefix));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsReference_Throws_OnEmptyPrefix(string? prefix)
    {
        Assert.ThrowsAny<ArgumentException>(() => SecretReference.IsReference("secret://x", prefix!));
    }

    [Fact]
    public void TryParse_ExtractsName_ForValidReference()
    {
        var success = SecretReference.TryParse("secret://myproduct/conn-1/password", Prefix, out var reference);

        Assert.True(success);
        Assert.Equal("myproduct/conn-1/password", reference!.Name);
        Assert.Equal(Prefix, reference.Prefix);
    }

    [Theory]
    [InlineData("plain-value")]
    [InlineData("secret://")]
    [InlineData("secret://   ")] // whitespace-only name
    [InlineData(null)]
    public void TryParse_Fails_ForNonReferenceOrEmptyName(string? value)
    {
        var success = SecretReference.TryParse(value, Prefix, out var reference);

        Assert.False(success);
        Assert.Null(reference);
    }

    [Fact]
    public void Parse_Throws_ForInvalidReference()
    {
        Assert.Throws<FormatException>(() => SecretReference.Parse("plain-value", Prefix));
    }

    [Fact]
    public void Build_CreatesToken_FromNameAndPrefix()
    {
        Assert.Equal("secret://a/b", SecretReference.Build("a/b", Prefix));
    }

    [Theory]
    [InlineData("", "secret://")]
    [InlineData("   ", "secret://")]
    [InlineData("name", "")]
    public void Build_Throws_OnInvalidArguments(string name, string prefix)
    {
        Assert.ThrowsAny<ArgumentException>(() => SecretReference.Build(name, prefix));
    }

    [Fact]
    public void BuildThenParse_RoundTripsName()
    {
        var token = SecretReference.Build("myproduct/conn-1/password", Prefix);

        var reference = SecretReference.Parse(token, Prefix);

        Assert.Equal("myproduct/conn-1/password", reference.Name);
        Assert.Equal(token, reference.ToString());
    }
}

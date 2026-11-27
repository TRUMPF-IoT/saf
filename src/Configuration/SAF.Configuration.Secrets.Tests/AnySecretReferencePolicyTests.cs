// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Configuration;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class AnySecretReferencePolicyTests
{
    private static readonly SecretReference Token = SecretReference.Parse("secret://db/pw", "secret://");

    private readonly AnySecretReferencePolicy _policy = new([
        new SecretReferenceAllowList([new("Db:Password", "db/pw")]),
        new KeyBoundSecretReferencePolicy()
    ]);

    [Fact]
    public void Allows_AReferenceTheFirstPolicyAllows()
        => Assert.True(_policy.Allows("Db:Password", Token));

    [Fact]
    public void Allows_AReferenceOnlyALaterPolicyAllows()
        => Assert.True(_policy.Allows("Db:Pw", Token));

    [Fact]
    public void Refuses_AReferenceNoPolicyAllows()
        => Assert.False(_policy.Allows("Db:Host", Token));

    [Fact]
    public void Refuses_EveryReference_WithoutPolicies()
        => Assert.False(new AnySecretReferencePolicy([]).Allows("Db:Pw", Token));

    [Fact]
    public void Constructor_Throws_OnNullPolicies()
        => Assert.Throws<ArgumentNullException>(() => new AnySecretReferencePolicy(null!));

    [Fact]
    public void Constructor_Throws_OnANullPolicy()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new AnySecretReferencePolicy([new KeyBoundSecretReferencePolicy(), null!]));

        Assert.Equal("policies", exception.ParamName);
    }

    [Fact]
    public void Allows_Throws_OnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => _policy.Allows(null!, Token));
        Assert.Throws<ArgumentNullException>(() => _policy.Allows("Db:Password", null!));
    }
}

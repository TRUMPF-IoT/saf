// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Configuration;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretReferenceAllowListTests
{
    private readonly SecretReferenceAllowList _allowList = new([
        new("Db:Password", "db/pw"),
        new("Db:User", "db/user"),
        new("Db:User", "db/admin")
    ]);

    [Fact]
    public void Allows_AListedNameInItsKey()
        => Assert.True(_allowList.Allows("Db:Password", Reference("db/pw")));

    [Fact]
    public void Allows_EveryNameListedForAKey()
    {
        Assert.True(_allowList.Allows("Db:User", Reference("db/user")));
        Assert.True(_allowList.Allows("Db:User", Reference("db/admin")));
    }

    [Fact]
    public void Allows_TheKeyWhateverItsCase()
        => Assert.True(_allowList.Allows("DB:PASSWORD", Reference("db/pw")));

    [Fact]
    public void Allows_TheNameWhateverItsCase_AsTheStoreLooksItUpThatWay()
        => Assert.True(_allowList.Allows("Db:Password", Reference("DB/Pw")));

    [Fact]
    public void Refuses_AnotherNameInAListedKey()
        => Assert.False(_allowList.Allows("Db:Password", Reference("db/user")));

    [Fact]
    public void Refuses_AListedNameInAnotherKey()
        => Assert.False(_allowList.Allows("Db:Host", Reference("db/pw")));

    [Fact]
    public void Refuses_EveryReference_WhenNothingIsListed()
        => Assert.False(new SecretReferenceAllowList([]).Allows("Db:Password", Reference("db/pw")));

    [Theory]
    [InlineData("", "db/pw")]
    [InlineData(" ", "db/pw")]
    [InlineData("Db:Password", "")]
    [InlineData("Db:Password", " ")]
    public void Constructor_Throws_OnAnEmptyKeyOrName(string key, string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SecretReferenceAllowList([new(key, name)]));

        Assert.Equal("references", exception.ParamName);
    }

    [Fact]
    public void Constructor_Throws_OnNullReferences()
        => Assert.Throws<ArgumentNullException>(() => new SecretReferenceAllowList(null!));

    [Fact]
    public void Allows_Throws_OnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => _allowList.Allows(null!, Reference("db/pw")));
        Assert.Throws<ArgumentNullException>(() => _allowList.Allows("Db:Password", null!));
    }

    private static SecretReference Reference(string name) => SecretReference.Parse("secret://" + name, "secret://");
}

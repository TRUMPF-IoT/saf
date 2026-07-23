// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

public class SharedAssemblyVersionComparerTests
{
    private readonly SharedAssemblyVersionComparer _comparer = new();

    [Theory]
    [InlineData("13.0.0.0", "12.0.0.0")]
    [InlineData("1.1.0.0", "1.0.0.0")]
    [InlineData("2.0.0.1", "2.0.0.0")]
    public void Compare_ReturnsHigher_WhenHostVersionIsGreater(string host, string requested)
    {
        var result = _comparer.Compare(Version.Parse(host), Version.Parse(requested));

        Assert.Equal(SharedAssemblyVersionRelation.Higher, result);
    }

    [Theory]
    [InlineData("12.0.0.0", "13.0.0.0")]
    [InlineData("1.0.0.0", "1.1.0.0")]
    [InlineData("2.0.0.0", "2.0.0.1")]
    public void Compare_ReturnsLower_WhenHostVersionIsSmaller(string host, string requested)
    {
        var result = _comparer.Compare(Version.Parse(host), Version.Parse(requested));

        Assert.Equal(SharedAssemblyVersionRelation.Lower, result);
    }

    [Fact]
    public void Compare_ReturnsEqual_WhenVersionsMatch()
    {
        var result = _comparer.Compare(new Version(1, 2, 3, 4), new Version(1, 2, 3, 4));

        Assert.Equal(SharedAssemblyVersionRelation.Equal, result);
    }

    [Fact]
    public void Compare_TreatsNullRequestedAsLowest_ReturnsHigher()
    {
        var result = _comparer.Compare(new Version(1, 0, 0, 0), requestedVersion: null);

        Assert.Equal(SharedAssemblyVersionRelation.Higher, result);
    }

    [Fact]
    public void Compare_TreatsNullRequestedAsLowest_ReturnsEqual_ForZeroHost()
    {
        var result = _comparer.Compare(new Version(0, 0, 0, 0), requestedVersion: null);

        Assert.Equal(SharedAssemblyVersionRelation.Equal, result);
    }

    [Fact]
    public void Compare_Throws_WhenHostVersionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _comparer.Compare(null!, new Version(1, 0)));
    }
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common.Tests;
using Xunit;

public class CdeConfigurationComparerTests
{
    private readonly CdeConfigurationComparer _comparer = new();

    [Fact]
    public void Equals_ReturnsTrue_ForEqualValues()
    {
        Assert.True(_comparer.Equals(CreateConfiguration(), CreateConfiguration()));
        Assert.Equal(_comparer.GetHashCode(CreateConfiguration()), _comparer.GetHashCode(CreateConfiguration()));
    }

    [Fact]
    public void Equals_ReturnsFalse_ForADifferentValue()
    {
        var other = CreateConfiguration();
        other.ScopeId = "other";

        Assert.False(_comparer.Equals(CreateConfiguration(), other));
    }

    [Fact]
    public void Equals_ReturnsFalse_ForADifferentCryptoLibSetting()
    {
        var other = CreateConfiguration();
        other.CryptoLibConfig!.DllName = "other.dll";

        Assert.False(_comparer.Equals(CreateConfiguration(), other));
    }

    [Fact]
    public void Equals_ReturnsFalse_ForADifferentAdditionalArgument()
    {
        var other = CreateConfiguration();
        other.AdditionalArguments["key"] = "other";

        Assert.False(_comparer.Equals(CreateConfiguration(), other));
    }

    [Fact]
    public void Equals_ReturnsTrue_ForAdditionalArgumentsInAnotherOrder()
    {
        var configuration = CreateConfiguration();
        configuration.AdditionalArguments = new Dictionary<string, string> { ["alpha"] = "1", ["beta"] = "2" };
        var reordered = CreateConfiguration();
        reordered.AdditionalArguments = new Dictionary<string, string> { ["beta"] = "2", ["alpha"] = "1" };

        Assert.True(_comparer.Equals(configuration, reordered));
        Assert.Equal(_comparer.GetHashCode(configuration), _comparer.GetHashCode(reordered));
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenOnlyOneIsNull()
    {
        Assert.False(_comparer.Equals(CreateConfiguration(), null));
        Assert.False(_comparer.Equals(null, CreateConfiguration()));
        Assert.True(_comparer.Equals(null, null));
    }

    private static CdeConfiguration CreateConfiguration() => new()
    {
        ScopeId = "4711",
        CryptoLibConfig = new CdeCryptoLibConfig { DllName = "crypto.dll" },
        AdditionalArguments = new Dictionary<string, string> { ["key"] = "value" }
    };
}
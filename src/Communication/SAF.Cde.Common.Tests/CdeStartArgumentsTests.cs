// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common.Tests;
using Xunit;

public class CdeStartArgumentsTests
{
    private readonly CdeStartArguments _startArguments = new();

    [Fact]
    public void Create_ReadsJsonStrictly_ByDefault()
    {
        var arguments = _startArguments.Create(new CdeConfiguration());

        Assert.Equal("True", arguments["UseStrictJSON"]);
    }

    [Fact]
    public void Create_ReadsJsonLeniently_WhenUseStrictJsonIsOff()
    {
        var arguments = _startArguments.Create(new CdeConfiguration { UseStrictJson = false });

        Assert.Equal("False", arguments["UseStrictJSON"]);
    }

    [Fact]
    public void Create_IgnoresAnAdditionalArgumentForAnOptionOfTheConfiguration()
    {
        var configuration = new CdeConfiguration
        {
            AdditionalArguments = new Dictionary<string, string> { ["UseStrictJSON"] = "False", ["key"] = "value" }
        };

        var arguments = _startArguments.Create(configuration);

        Assert.Equal("True", arguments["UseStrictJSON"]);
        Assert.Equal("value", arguments["key"]);
    }

    [Fact]
    public void Create_PassesTheScopeIdAsEasyScope()
    {
        var arguments = _startArguments.Create(new CdeConfiguration { ScopeId = "4711" });

        Assert.Equal("4711", arguments["EasyScope"]);
    }
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common.Tests;
using Xunit;

public class CdeStartArgumentsTests
{
    [Fact]
    public void Create_ReadsJsonStrictly_ByDefault()
    {
        var arguments = new CdeStartArguments(new CdeConfiguration()).Create();

        Assert.Equal("True", arguments["UseStrictJSON"]);
    }

    [Fact]
    public void Create_ReadsJsonLeniently_WhenUseStrictJsonIsOff()
    {
        var arguments = new CdeStartArguments(new CdeConfiguration { UseStrictJson = false }).Create();

        Assert.Equal("False", arguments["UseStrictJSON"]);
    }

    [Fact]
    public void Create_IgnoresAnAdditionalArgumentForAnOptionOfTheConfiguration()
    {
        var configuration = new CdeConfiguration
        {
            AdditionalArguments = new Dictionary<string, string> { ["UseStrictJSON"] = "False", ["key"] = "value" }
        };

        var arguments = new CdeStartArguments(configuration).Create();

        Assert.Equal("True", arguments["UseStrictJSON"]);
        Assert.Equal("value", arguments["key"]);
    }

    [Fact]
    public void Create_PassesTheScopeIdAsEasyScope()
    {
        var arguments = new CdeStartArguments(new CdeConfiguration { ScopeId = "4711" }).Create();

        Assert.Equal("4711", arguments["EasyScope"]);
    }
}

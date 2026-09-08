// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretTargetNameTests
{
    [Theory]
    [InlineData("myapp", "conn/pw", "myapp/conn/pw")]
    [InlineData("", "conn/pw", "conn/pw")]
    [InlineData(null, "conn/pw", "conn/pw")]
    public void Build_PrependsNamespace_UnlessEmpty(string? ns, string name, string expected)
    {
        Assert.Equal(expected, SecretTargetName.Build(ns, name));
    }

    [Theory]
    [InlineData("MyApp", "Conn/PW", "myapp/conn/pw")]
    [InlineData(null, "Conn/PW", "conn/pw")]
    public void Build_IsCaseInsensitive(string? ns, string name, string expected)
    {
        Assert.Equal(expected, SecretTargetName.Build(ns, name));
    }

    [Fact]
    public void Build_ProducesTheSameTargetName_RegardlessOfCallerCasing()
    {
        var lower = SecretTargetName.Build("myapp", "conn/pw");
        var mixed = SecretTargetName.Build("MyApp", "Conn/Pw");
        var upper = SecretTargetName.Build("MYAPP", "CONN/PW");

        Assert.Equal(lower, mixed);
        Assert.Equal(lower, upper);
    }
}

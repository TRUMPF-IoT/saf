// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Tests;

using Xunit;

public class WildcardMatcherTests
{
    [Theory]
    [InlineData("sensor/temperature", "sensor/*")]
    [InlineData("sensor/a/value", "sensor/?/value")]
    [InlineData("any/topic", "*")]
    public void IsMatch_WhenPatternMatches_ReturnsTrue(string value, string pattern)
    {
        var result = value.IsMatch(pattern);

        Assert.True(result);
    }

    [Theory]
    [InlineData("sensor/temperature", "device/*")]
    [InlineData("sensor/ab/value", "sensor/?/value")]
    public void IsMatch_WhenPatternDoesNotMatch_ReturnsFalse(string value, string pattern)
    {
        var result = value.IsMatch(pattern);

        Assert.False(result);
    }
}

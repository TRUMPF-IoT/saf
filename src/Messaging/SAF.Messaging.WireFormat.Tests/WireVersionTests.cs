// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat.Tests;

using Xunit;

public class WireVersionTests
{
    private readonly WireVersion _wireVersion = new();

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("2.0.0", 2)]
    [InlineData("2.7", 2)]
    [InlineData("10.0.0", 10)]
    public void TryGetMajor_ReturnsTheMajorVersion(string? version, int expected)
    {
        Assert.True(_wireVersion.TryGetMajor(version, out var major));
        Assert.Equal(expected, major);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2")]
    public void TryGetMajor_RejectsUnparsableVersions(string version)
        => Assert.False(_wireVersion.TryGetMajor(version, out _));
}

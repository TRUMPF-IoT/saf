// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Tests;

using Xunit;

public class CharUtilitiesTests
{
    [Fact]
    public void CharReplacerFunc_WhenSourceIsWhitespace_ReturnsOriginalValue()
    {
        const string source = "   ";

        var result = CharUtilities.CharReplacerFunc(source, static (character, _) => character);

        Assert.Equal(source, result);
    }

    [Fact]
    public void CharReplacerFunc_WhenSourceHasCharacters_AppliesReplacementFunction()
    {
        var hasNextArguments = new List<bool>();

        var result = CharUtilities.CharReplacerFunc("abc", (character, hasNext) =>
        {
            hasNextArguments.Add(hasNext);
            return char.ToUpperInvariant(character);
        });

        Assert.Equal("ABC", result);
        Assert.Equal([true, true, false], hasNextArguments);
    }
}

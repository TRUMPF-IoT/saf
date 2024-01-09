// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Tests;
using Xunit;

public class WildcardMatcherTests
{
    [Fact]
    public void IsMatchWithMiddlePatternMatchesCorrectSubTopicsOnly()
    {
        const string pattern = "begin/*/end";

        Assert.True("begin/one/end".IsMatch(pattern));
        Assert.True("begin/one/two/end".IsMatch(pattern));

        Assert.False("begin/one".IsMatch(pattern));
        Assert.False("wrong/one/end".IsMatch(pattern));
        Assert.False("begin/one/two/wrong".IsMatch(pattern));
    }
}
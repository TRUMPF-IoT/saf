// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities;
using Microsoft.Extensions.Logging;
using NSubstitute;

public static class MockLoggerAssertionExtensions
{
    public static void AssertLogged(this MockLogger substitute, LogLevel level)
        => substitute.Received().Log(level, Arg.Any<string>());

    public static void AssertLogged<T>(this MockLogger<T> substitute, LogLevel level)
        => substitute.Received().Log(level, Arg.Any<string>());

    public static void AssertLogged(this MockLogger substitute, LogLevel level, Func<string, bool> messagePredicate)
        => substitute.Received().Log(level, Arg.Is<string>(message => messagePredicate(message!)));

    public static void AssertLogged(this MockLogger substitute, Func<string, bool> messagePredicate)
        => substitute.Received().Log(Arg.Any<LogLevel>(), Arg.Is<string>(message => messagePredicate(message!)));

    public static void AssertLoggedOnce(this MockLogger substitute, LogLevel level)
        => substitute.Received(1).Log(level, Arg.Any<string>());

    public static void AssertNotLogged(this MockLogger substitute, LogLevel level)
        => substitute.DidNotReceive().Log(level, Arg.Any<string>());

    public static void AssertNotLogged(this MockLogger substitute, Func<string, bool> messagePredicate)
        => substitute.DidNotReceive().Log(Arg.Any<LogLevel>(), Arg.Is<string>(message => messagePredicate(message!)));
}
// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities;
using Microsoft.Extensions.Logging;
using NSubstitute;

public static class MockLoggerAssertionExtensions
{
    public static void AssertLogged(this MockLogger substitute, LogLevel level)
        => substitute.Received().Log(level, Arg.Any<string>());

    public static void AssertNotLogged(this MockLogger substitute, LogLevel level)
        => substitute.DidNotReceive().Log(level, Arg.Any<string>());

    public static void AssertLogged<T>(this MockLogger<T> substitute, LogLevel level)
        => substitute.Received().Log(level, Arg.Any<string>());
}
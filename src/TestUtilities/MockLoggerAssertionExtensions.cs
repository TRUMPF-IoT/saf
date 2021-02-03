// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TestUtilities
{
    public static class MockLoggerAssertionExtensions
    {
        public static void AssertLogged(this MockLogger substitute, LogLevel level)
            => substitute.Received().Log(level, Arg.Any<string>());

        public static void AssertNotLogged(this MockLogger substitute, LogLevel level)
            => substitute.DidNotReceive().Log(level, Arg.Any<string>());

        public static void AssertLogged<T>(this MockLogger<T> substitute, LogLevel level)
            => substitute.Received().Log(level, Arg.Any<string>());
    }
}
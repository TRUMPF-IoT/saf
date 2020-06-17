// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TestUtilities
{
    public static class LoggerAssertionExtensions
    {
        public static void AssertLogged(this ILogger substitute, LogLevel level)
            => substitute.Received().Log(level, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());

        public static void AssertNotLogged(this ILogger substitute, LogLevel level)
            => substitute.DidNotReceive().Log(level, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());

        public static void AssertLogged<T>(this ILogger<T> substitute, LogLevel level)
            => substitute.Received().Log(level, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
    }
}
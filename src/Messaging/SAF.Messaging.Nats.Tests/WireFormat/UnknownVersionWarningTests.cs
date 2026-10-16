// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Messaging.Nats.WireFormat;
using TestUtilities;
using Xunit;

public class UnknownVersionWarningTests
{
    [Fact]
    public void Report_WarnsOncePerVersion()
    {
        var logger = Substitute.For<MockLogger>();
        var warning = new UnknownVersionWarning(logger);

        warning.Report("3.0.0", "a");
        warning.Report("3.0.0", "b");
        warning.Report("4.0.0", "a");

        logger.Received(2).Log(LogLevel.Warning, Arg.Any<string>());
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains("3.0.0") && m.Contains("a")));
    }

    [Fact]
    public void Report_StopsWarningOnceTheVersionLimitIsReached()
    {
        var logger = Substitute.For<MockLogger>();
        var warning = new UnknownVersionWarning(logger);

        for (var i = 0; i < 40; i++) warning.Report($"{i + 3}.0.0", "topic");

        logger.Received(32).Log(LogLevel.Warning, Arg.Any<string>());
    }
}

// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using NSubstitute;
using System;
using System.Reflection;
using Xunit;

namespace SAF.Hosting.Cde.Tests
{
    public class TestHostingCde
    {
        [Fact]
        public void RunLogger()
        {
            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
            {
                DebugLevel = eDEBUG_LEVELS.ESSENTIALS
            };
            //TheBaseAssets.MySYSLOG = Substitute.For<TheSystemMessageLog>();

            LoggerProvider lp = new ();
            var logger = (Logger)lp.CreateLogger("Test");
            Assert.NotNull(logger);

            Assert.True(logger.IsEnabled(LogLevel.Critical));
            Assert.True(logger.IsEnabled(LogLevel.Error));
            Assert.True(logger.IsEnabled(LogLevel.Warning));
            Assert.False(logger.IsEnabled(LogLevel.Information));
            Assert.False(logger.IsEnabled(LogLevel.Debug));
            Assert.False(logger.IsEnabled(LogLevel.Trace));

            //EventId eventId = new EventId(666, "Test-Fehler");
            //logger.Log(LogLevel.Error, eventId, null, "State", null);
            //TheBaseAssets.MySYSLOG.Received().WriteToLog(eDEBUG_LEVELS.ESSENTIALS, 0, "Fehler", "Fehler");

            TheBaseAssets.MyServiceHostInfo.DebugLevel = eDEBUG_LEVELS.VERBOSE;
            Assert.True(logger.IsEnabled(LogLevel.Warning));
            Assert.True(logger.IsEnabled(LogLevel.Information));
            Assert.False(logger.IsEnabled(LogLevel.Debug));
            Assert.False(logger.IsEnabled(LogLevel.Trace));

            TheBaseAssets.MyServiceHostInfo.DebugLevel = eDEBUG_LEVELS.FULLVERBOSE;
            Assert.True(logger.IsEnabled(LogLevel.Information));
            Assert.True(logger.IsEnabled(LogLevel.Debug));
            Assert.False(logger.IsEnabled(LogLevel.Trace));

            TheBaseAssets.MyServiceHostInfo.DebugLevel = eDEBUG_LEVELS.EVERYTHING;
            Assert.True(logger.IsEnabled(LogLevel.Debug));
            Assert.True(logger.IsEnabled(LogLevel.Trace));

            TheBaseAssets.MyServiceHostInfo.DebugLevel = eDEBUG_LEVELS.OFF;
            Assert.False(logger.IsEnabled(LogLevel.Critical));
            Assert.False(logger.IsEnabled(LogLevel.Error));
            Assert.False(logger.IsEnabled(LogLevel.Warning));
            Assert.False(logger.IsEnabled(LogLevel.Information));
            Assert.False(logger.IsEnabled(LogLevel.Debug));
            Assert.False(logger.IsEnabled(LogLevel.Trace));
        }

        [Fact]
        public void RunServiceHost()
        {
            Assert.Throws<NullReferenceException>(() => new ServiceHost());

        }
    }
}

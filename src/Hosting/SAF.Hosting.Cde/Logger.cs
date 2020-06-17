// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using Microsoft.Extensions.Logging;
using nsCDEngine.BaseClasses;

namespace SAF.Hosting.Cde
{
    public class LoggerProvider : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName);
        }
    }

    internal class Logger : ILogger
    {
        public Logger(string logName)
        {
            _logName = logName;
        }

        private const int LogId = 0;
        private readonly string _logName;

        private static bool IsLevelActive(eDEBUG_LEVELS logLevel)
        {
            return TheBaseAssets.MyServiceHostInfo != null && logLevel <= TheBaseAssets.MyServiceHostInfo.DebugLevel;
        }

        private void Log(eDEBUG_LEVELS logLevel, eMsgLevel messageLevel, string message, string payload = null)
        {
            if(IsLevelActive(logLevel))
                TheBaseAssets.MySYSLOG?.WriteToLog(LogId, new TSM(_logName, message, messageLevel, payload));
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Log(Convert(logLevel), eMsgLevel.l4_Message, $"{state}", exception?.ToString());
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return IsLevelActive(Convert(logLevel));
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new Scope();
        }

        private static eDEBUG_LEVELS Convert(LogLevel logLevel)
        {
            switch(logLevel)
            {
                case LogLevel.None: return eDEBUG_LEVELS.OFF;
                case LogLevel.Critical: return eDEBUG_LEVELS.ESSENTIALS;
                case LogLevel.Error: return eDEBUG_LEVELS.ESSENTIALS;
                case LogLevel.Warning: return eDEBUG_LEVELS.ESSENTIALS;
                case LogLevel.Information: return eDEBUG_LEVELS.VERBOSE;
                case LogLevel.Debug: return eDEBUG_LEVELS.FULLVERBOSE;
                case LogLevel.Trace: return eDEBUG_LEVELS.EVERYTHING;
                default: return eDEBUG_LEVELS.OFF;
            }
        }

        public static LogLevel Convert(eDEBUG_LEVELS logLevel)
        {
            switch (logLevel)
            {
                case eDEBUG_LEVELS.OFF:
                    return LogLevel.None;
                case eDEBUG_LEVELS.ESSENTIALS:
                    return LogLevel.Warning;
                case eDEBUG_LEVELS.VERBOSE:
                    return LogLevel.Information;
                case eDEBUG_LEVELS.FULLVERBOSE:
                    return LogLevel.Debug;
                case eDEBUG_LEVELS.EVERYTHING:
                    return LogLevel.Trace;
                default: return LogLevel.None;
            }
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
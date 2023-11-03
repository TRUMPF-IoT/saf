// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;

namespace SAF.Communication.Cde.Utils;

public class Logger
{
    private const int LogId = 0;
    private readonly string _logName;

    public Logger(string logName)
    {
        _logName = logName;
    }

    public Logger(Type type) : this(type.FullName ?? type.Name)
    { }

    private static bool IsLevelActive(eDEBUG_LEVELS logLevel)
    {
        return TheBaseAssets.MyServiceHostInfo != null && logLevel <= TheBaseAssets.MyServiceHostInfo.DebugLevel;
    }

    public void Log(eDEBUG_LEVELS logLevel, eMsgLevel messageLevel, string message, string? payload = null)
    {
        if (IsLevelActive(logLevel))
            TheBaseAssets.MySYSLOG?.WriteToLog(LogId, new TSM(_logName, message, messageLevel, payload));
    }
}

public static class LoggerExtensions
{
    public static void LogCritical(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.ESSENTIALS, eMsgLevel.l1_Error, message, ex?.ToString());

    public static void LogError(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.ESSENTIALS, eMsgLevel.l2_Warning, message, ex?.ToString());

    public static void LogWarning(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.ESSENTIALS, eMsgLevel.l3_ImportantMessage, message, ex?.ToString());

    public static void LogInformation(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.VERBOSE, eMsgLevel.l4_Message, message, ex?.ToString());

    public static void LogDebug(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.FULLVERBOSE, eMsgLevel.l4_Message, message, ex?.ToString());

    public static void LogTrace(this Logger logger, string message, Exception? ex = null)
        => logger.Log(eDEBUG_LEVELS.EVERYTHING, eMsgLevel.l4_Message, message, ex?.ToString());
}
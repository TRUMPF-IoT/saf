// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SAF.Communication.Cde.TestHost
{
    internal class Program
    {
        private static void Main()
        {
            Thread.CurrentThread.Name = "Main thread";

            var settings = ConfigurationManager.AppSettings.AllKeys.ToDictionary(key => key, key => ConfigurationManager.AppSettings[key]);
            AssignConfigValue(settings, "ApplicationID", value => TheScopeManager.SetApplicationID(value));

            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
            {
                Title = GetConfigValue(settings, "ApplicationTitle"),
                ApplicationName = GetConfigValue(settings, "ApplicationName"),
                ApplicationTitle = GetConfigValue(settings, "PortalTitle"),
                DebugLevel = GetDebugLevel(settings, "DebugLevel", eDEBUG_LEVELS.ESSENTIALS),
                MyStationPort = Convert.ToUInt16(GetConfigValue(settings, "HTTPPort")),
                MyStationWSPort = Convert.ToUInt16(GetConfigValue(settings, "WSPort")),
                cdeMID = TheCommonUtils.CGuid(GetConfigValue(settings, "StorageID")),
                FailOnAdminCheck = Convert.ToBoolean(GetConfigValue(settings, "FailOnAdminCheck")),
                CloudServiceRoute = GetConfigValue(settings, "CloudServiceRoutes"),
                LocalServiceRoute = GetConfigValue(settings, "LocalServiceRoutes"),
                IsCloudService = Convert.ToBoolean(GetConfigValue(settings, "IsCloudService")),
                ISMMainExecutable = "Host",
                CurrentVersion = 1.0001,
                AllowLocalHost = Convert.ToBoolean(GetConfigValue(settings, "AllowLocalHost"))
            };

            TheBaseAssets.MyServiceHostInfo.IgnoredEngines.AddRange(GetConfigValue(settings, "LogIgnore").Split(';'));

            Debug.WriteLine($"Ports: {TheBaseAssets.MyServiceHostInfo.MyStationWSPort}/{TheBaseAssets.MyServiceHostInfo.MyStationPort}");

            var arguments = new Dictionary<string, string>
            {
                { "DontVerifyTrust", $"{Convert.ToBoolean(GetConfigValue(settings, "DontVerifyTrust"))}" },
                { "UseUserMapper", $"{Convert.ToBoolean(GetConfigValue(settings, "UseUserMapper"))}" },
                { "UseRandomDeviceID", $"{Convert.ToBoolean(GetConfigValue(settings, "UseRandomDeviceID"))}" },
                { "AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService },
                { "SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService }
            };

            var scopeId = ConfigurationManager.AppSettings["ScopeID"];
            if (!string.IsNullOrEmpty(scopeId))
                if (scopeId.Length == 8)
                {
                    Console.WriteLine("Current Scope:" + scopeId);
                    TheScopeManager.SetScopeIDFromEasyID(scopeId);
                }

            var app = new TheBaseApplication();
            if (!app.StartBaseApplication(null, arguments))
                return;

            while (true)
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Escape) break;
            }

            app.Shutdown(false, true);
        }

        private static void AssignConfigValue(IDictionary<string, string> settings, string key, Action<string> fn)
        {
            if (!settings.TryGetValue(key, out var value)) throw new ConfigurationErrorsException(key + " not found in configuration.");

            fn(value);
        }

        private static string GetConfigValue(IDictionary<string, string> settings, string key)
        {
            if (!settings.TryGetValue(key, out var value)) throw new ConfigurationErrorsException(key + " not found in configuration.");

            return value;
        }

        private static eDEBUG_LEVELS GetDebugLevel(IDictionary<string, string> settings, string key, eDEBUG_LEVELS defaultValue)
        {
            var debugLevel = (eDEBUG_LEVELS)Enum.Parse(typeof(eDEBUG_LEVELS), GetConfigValue(settings, key));
            return Enum.IsDefined(typeof(eDEBUG_LEVELS), debugLevel)
                ? debugLevel
                : defaultValue;
        }
    }
}

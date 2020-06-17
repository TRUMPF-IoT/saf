// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SAF.Messaging.Cde
{
    internal class CdeApplication : IDisposable
    {
        private readonly ILogger<CdeApplication> _log;
        private readonly CdeConfiguration _config;

        private TheBaseApplication _cdeApp;

        public CdeApplication(ILogger<CdeApplication> log, CdeConfiguration config)
        {
            _log = log ?? NullLogger<CdeApplication>.Instance;
            _config = config;
        }

        public void Start()
        {
            _log.LogInformation("Starting CDE...");
#pragma warning disable IDE0067 // Dispose objects before losing scope
            _cdeApp = StartCde(_config).Result;
#pragma warning restore IDE0067 // Dispose objects before losing scope
            _log.LogInformation("... CDE started!");
        }

        public void Dispose()
        {
            _log.LogInformation("Shutting down CDE...");
            _cdeApp?.Shutdown(false, true);
            _cdeApp?.Dispose();
            _log.LogInformation("...CDE shut down.");
        }

        private async Task<TheBaseApplication> StartCde(CdeConfiguration config)
        {
            TheScopeManager.SetApplicationID(config.ApplicationId);

            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
            {
                Title = config.ApplicationTitle,
                ApplicationName = config.ApplicationName,
                ApplicationTitle = config.PortalTitle,
                DebugLevel = GetDebugLevel(config.DebugLevel, eDEBUG_LEVELS.ESSENTIALS),
                MyStationPort = config.HttpPort,
                MyStationWSPort = config.WsPort,
                cdeMID = TheCommonUtils.CGuid(config.StorageId),
                FailOnAdminCheck = config.FailOnAdminCheck,
                CloudServiceRoute = config.CloudServiceRoutes,
                LocalServiceRoute = config.LocalServiceRoutes,
                IsCloudService = config.IsCloudService,
                ISMMainExecutable = Assembly.GetEntryAssembly()?.GetName().Name,
                CurrentVersion = config.ApplicationVersion,
                AllowLocalHost = config.AllowLocalHost,
                PreShutDownDelay = config.PreShutdownDelay
            };

            TheBaseAssets.MyServiceHostInfo.IgnoredEngines.AddRange(config.LogIgnore.Split(';'));
            ApplyScopeId(config);

            IDictionary<string, string> arguments = new Dictionary<string, string>
            {
                { "DontVerifyTrust", $"{config.DontVerifyTrust}" },
                { "UseUserMapper", $"{config.UseUserMapper}" },
                { "UseRandomDeviceID", $"{config.UseRandomDeviceId}" },
                { "AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService },
                { "SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService }
            };
            arguments = MergeDictionaries(arguments, config.AdditionalArguments);

            var app = new TheBaseApplication();
            if(!app.StartBaseApplication(null, arguments))
            {
                const string error = "Failed to start CDE base application!";
                _log.LogCritical(error);
                throw new InvalidOperationException(error);
            }

            await TheBaseEngine.WaitForEnginesStartedAsync();
            await TheBaseEngine.WaitForStorageReadinessAsync(true);

            return app;
        }

        private static void ApplyScopeId(CdeConfiguration config)
        {
            if (config.UseRandomScopeId)
            {
                TheBaseAssets.MyServiceHostInfo.SealID = TheScopeManager.GenerateNewScopeID();
                TheScopeManager.SetScopeIDFromEasyID(TheBaseAssets.MyServiceHostInfo.SealID);
            }
            else
            {
                var scopeId = config.ScopeId;
                if (!string.IsNullOrEmpty(scopeId))
                {
                    TheScopeManager.SetScopeIDFromEasyID(scopeId);
                }
            }
        }

        private static IDictionary<string, string> MergeDictionaries(IDictionary<string, string> dict1, IDictionary<string, string> dict2)
        {
            return dict1
                .Concat(dict2.Where(kvp => !dict1.ContainsKey(kvp.Key)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static eDEBUG_LEVELS GetDebugLevel(string debugLevel, eDEBUG_LEVELS defaultValue)
        {
            var level = (eDEBUG_LEVELS)Enum.Parse(typeof(eDEBUG_LEVELS), debugLevel);
            return Enum.IsDefined(typeof(eDEBUG_LEVELS), level)
                ? level
                : defaultValue;
        }
    }
}
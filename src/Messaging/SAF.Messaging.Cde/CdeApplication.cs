// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
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
            _cdeApp = StartCde(_config).Result;
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
            try
            {
                LoadAlternateCryptoLib(config.CryptoLibConfig);

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
                IDictionary<string, string> arguments = new Dictionary<string, string>
                {
                    { "DontVerifyTrust", $"{config.DontVerifyTrust}" },
                    { "UseUserMapper", $"{config.UseUserMapper}" },
                    { "UseRandomDeviceID", $"{config.UseRandomDeviceId}" },
                    { "AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService },
                    { "SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService }
                };

                if (!string.IsNullOrEmpty(config.ScopeId))
                    arguments["EasyScope"] = config.ScopeId;

                arguments = MergeDictionaries(arguments, config.AdditionalArguments);

                var app = new TheBaseApplication();
                var startTime = DateTimeOffset.UtcNow;
                if (!app.StartBaseApplication(null, arguments))
                    throw new InvalidOperationException("Failed to start CDE base application!");

                if (config.UseRandomScopeId && string.IsNullOrEmpty(TheScopeManager.GetScrambledScopeID()))
                {
                    if (!TheScopeManager.SetScopeIDFromEasyID(TheScopeManager.GenerateNewScopeID()))
                        throw new InvalidOperationException("Failed to apply random scope!");
                }

                _log.LogDebug("Started CDE Base application after {milliseconds} ms", DateTimeOffset.UtcNow.Subtract(startTime).TotalMilliseconds);
                startTime = DateTimeOffset.UtcNow;

                await TheBaseEngine.WaitForEnginesStartedAsync();

                _log.LogDebug("Started CDE engines after {milliseconds} ms", DateTimeOffset.UtcNow.Subtract(startTime).TotalMilliseconds);
                startTime = DateTimeOffset.UtcNow;

                await TheBaseEngine.WaitForStorageReadinessAsync(true);

                _log.LogDebug("Started CDE Storage after {milliseconds} ms", DateTimeOffset.UtcNow.Subtract(startTime).TotalMilliseconds);

                return app;
            }
            catch (Exception ex)
            {
                _log.LogCritical(ex, "Failed to start CDE");
                throw;
            }
        }

        private void LoadAlternateCryptoLib(CdeCryptoLibConfig cryptoLibConfig)
        {
            if (string.IsNullOrWhiteSpace(cryptoLibConfig?.DllName)) return;

            var error = TheBaseAssets.LoadCrypto(cryptoLibConfig.DllName, null, cryptoLibConfig.DontVerifyTrust, null,
                cryptoLibConfig.VerifyTrustPath, cryptoLibConfig.DontVerifyIntegrity);
            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"Failed loading configured crypto DLL: '{error}'");
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
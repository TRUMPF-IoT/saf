// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using nsCDEngine.BaseClasses;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;

namespace SAF.Messaging.Cde.Tests
{
    public class CdeFixture : IDisposable
    {
        private readonly TheBaseApplication _app;
        private Storage _storage;

        internal Storage Storage => _storage ?? (_storage = new Storage(null));

        public CdeFixture()
        {
            _app = InitializeCde();
        }

        public void Dispose()
        {
            _storage?.Dispose();
            _app?.Shutdown(false, true);
        }

        private TheBaseApplication InitializeCde()
        {
            TheScopeManager.SetApplicationID("/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?");
            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
            {
                Title = "SAF Tests",
                ApplicationName = "SAF Tests",
                ApplicationTitle = "SAF Tests",
                DebugLevel = eDEBUG_LEVELS.ESSENTIALS,
                MyStationPort = 8080,
                MyStationWSPort = 8080,
                cdeMID = new Guid("{DE4E9E30-1241-4E85-B5FC-1606910F0709}"),
                FailOnAdminCheck = false,
                CloudServiceRoute = string.Empty,
                LocalServiceRoute = string.Empty,
                IsCloudService = false,
                // ISMMainExecutable = Path.GetFileNameWithoutExtension(entryAssembly),
                CurrentVersion = 1.0001,
                AllowLocalHost = true
            };

            Debug.WriteLine($"Ports: {TheBaseAssets.MyServiceHostInfo.MyStationWSPort}/{TheBaseAssets.MyServiceHostInfo.MyStationPort}");

            var arguments = new Dictionary<string, string>
            {
                { "DontVerifyTrust", $"{true}" },
                { "UseUserMapper", $"{false}" },
                { "UseRandomDeviceID", $"{false}" },
                { "AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService },
                { "SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService }
            };

            const string scopeId = "12345678";
            Debug.WriteLine("Current Scope:" + scopeId);
            TheScopeManager.SetScopeIDFromEasyID(scopeId);

            var app = new TheBaseApplication();
            if (!app.StartBaseApplication(null, arguments))
                throw new Exception("TheBaseApplication.StartBaseApplication failed!");

            return app;
        }
    }
}
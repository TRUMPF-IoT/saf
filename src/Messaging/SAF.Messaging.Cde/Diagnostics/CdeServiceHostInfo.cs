// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using System;
using System.IO;

namespace SAF.Messaging.Cde.Diagnostics
{
    internal class CdeServiceHostInfo
    {
        public CdeServiceHostInfo()
        {
            var (buildNumber, buildDate) = BuildInformationFromCdeType(TheBaseAssets.MyServiceHostInfo.GetType());
            BuildNumber = buildNumber;
            BuildDate = buildDate;
        }

        private static (string buildNumber, DateTimeOffset buildDate) BuildInformationFromCdeType(Type cdeType)
        {
            var cdeAssembly = cdeType.Assembly;
            var cdeVersion = cdeAssembly.GetName().Version;
            var cdeFileInfo = new FileInfo(cdeAssembly.Location);
            return (cdeVersion?.ToString() ?? string.Empty, cdeFileInfo.LastWriteTimeUtc);
        }

        public int DefaultLcid => TheBaseAssets.MyServiceHostInfo.DefaultLCID;
        public double Version => TheBaseAssets.MyServiceHostInfo.CurrentVersion;
        public string ApplicationName => TheBaseAssets.MyServiceHostInfo.ApplicationName;
        public string NodeName => TheBaseAssets.MyServiceHostInfo.NodeName;
        public Guid DeviceId => TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
        public bool VerifyTrust => !TheBaseAssets.MyServiceHostInfo.DontVerifyTrust;
        public string BuildNumber { get; }
        public DateTimeOffset BuildDate { get; }
        public string Platform => TheBaseAssets.MyServiceHostInfo.cdePlatform.ToString();
        public DateTimeOffset StartTime => TheBaseAssets.MyServiceHostInfo.EntryDate;

        public string CloudServiceRoute => TheBaseAssets.MyServiceHostInfo.CloudServiceRoute;
    }
}
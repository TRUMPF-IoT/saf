// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using System;

namespace SAF.Messaging.Cde.Diagnostics
{
    internal class CdeServiceHostInfo
    {
        public CdeServiceHostInfo()
        {
            var (buildNumber, buildDate) = BuildInformationFromVersionString(TheBaseAssets.MyServiceHostInfo.Version);
            BuildNumber = buildNumber;
            BuildDate = buildDate;
        }

        private static (string buildNumber, DateTimeOffset buildDate) BuildInformationFromVersionString(string version)
        {
            var versionParts = version.Split(' ');
            var buildDateString = $"{versionParts[1].Trim('(', ')')} {versionParts[2].Trim('(', ')')}";
            var parsedTime = DateTime.Parse(buildDateString);
            return (versionParts[0], DateTime.SpecifyKind(parsedTime, DateTimeKind.Utc));
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
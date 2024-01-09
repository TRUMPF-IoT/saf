// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Diagnostics;
using nsCDEngine.BaseClasses;
using System.Diagnostics;
using Hosting.Abstractions;

internal class CdeNodeInfo
{
    private readonly IServiceHostInfo _hostInfo;

    public CdeNodeInfo(IServiceHostInfo hostInfo)
    {
        _hostInfo = hostInfo;
    }

    public string SafHostId => _hostInfo.Id;
    public CdeServiceHostInfo ServiceHostInfo { get; } = new();
    public CdeVersionInfo CdeVersionInfo { get; } = new();

    public IEnumerable<CdePluginInfo> CdePlugIns { get; } = ReadPluginInfos();

    private static IEnumerable<CdePluginInfo> ReadPluginInfos()
    {
        return TheBaseAssets.MyCDEPluginTypes
            .Select(pt => CreatePluginInfo(pt.Key, pt.Value))
            .Where(pi => pi != null)
            .Cast<CdePluginInfo>();
    }

    private static CdePluginInfo? CreatePluginInfo(string plugInKey, Type plugInType)
    {
        if(string.IsNullOrEmpty(plugInType.Assembly.Location)) return null;
        var fvi = FileVersionInfo.GetVersionInfo(plugInType.Assembly.Location);
        var info = new CdePluginInfo
        {
            Name = plugInKey,
            Version = fvi.ProductVersion ?? string.Empty,
            BuildNumber = plugInType.Assembly.GetName().Version?.ToString() ?? string.Empty,
            BuildDate = File.GetLastWriteTimeUtc(plugInType.Assembly.Location)
        };
        return info;
    }
}
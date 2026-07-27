// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Diagnostics;

using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;

public class SafNodeInfo
{
    public SafNodeInfo(IServiceHostInfo? hostInfo, IEnumerable<IPluginManifest> pluginManifests)
    {
        HostId = hostInfo?.Id ?? Environment.MachineName;
        UpSince = hostInfo?.UpSince ?? DateTimeOffset.Now;
        SafServices = ReadServiceInfos(pluginManifests);
    }

    public string HostId { get; }

    public SafVersionInfo SafVersionInfo { get; } = new();

    public IEnumerable<SafServiceInfo> SafServices { get; }

    public DateTimeOffset UpSince { get; }

    private static IEnumerable<SafServiceInfo> ReadServiceInfos(IEnumerable<IPluginManifest> pluginManifests)
        => pluginManifests
            .Select(m =>
            {
                try
                {
                    return new SafServiceInfo(m);
                }
                catch
                {
                    return null;
                }
            })
            .Where(si => si != null)
            .Cast<SafServiceInfo>();
}

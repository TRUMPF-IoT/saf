// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Diagnostics;
using Contracts;

internal class SafNodeInfo
{
    private readonly IServiceHostInfo _hostInfo;

    public SafNodeInfo(IServiceHostInfo hostInfo, IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
    {
        _hostInfo = hostInfo;
        SafServices = ReadServiceInfos(serviceAssemblies);
    }

    public string HostId => _hostInfo.Id;
    public SafVersionInfo SafVersionInfo { get; } = new();
    public IEnumerable<SafServiceInfo> SafServices { get; }
    public DateTimeOffset UpSince => _hostInfo.UpSince;

    private static IEnumerable<SafServiceInfo> ReadServiceInfos(IEnumerable<IServiceAssemblyManifest> serviceAssemblies) =>
        serviceAssemblies
            .Select(a => { try { return new SafServiceInfo(a); } catch { return null; } })
            .Where(si => si != null)
            .Cast<SafServiceInfo>();
}
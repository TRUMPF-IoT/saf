// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using System.Reflection;

namespace SAF.Hosting;

public class ServiceHostInfo : IServiceHostInfo
{
    private readonly Func<string> _initializeId;

    private string? _id;

    internal ServiceHostInfo(Func<string> initializeId)
    {
        _initializeId = initializeId;
    }

    public string Id
    {
        get => _id ??= _initializeId();
        set => _id = value;
    }

    public string ServiceHostType { get; set; } = "Unknown";

    public string FileSystemUserBasePath { get; set; } = "tempfs";

    public string FileSystemInstallationPath { get; set; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    public DateTimeOffset UpSince { get; } = DateTimeOffset.Now;
}
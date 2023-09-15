// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using System;

namespace SAF.Hosting;

public class HostInfo : IHostInfo
{
    private readonly Func<string> _initializeId;

    private string _id;

    internal HostInfo(Func<string> initializeId)
    {
        _initializeId = initializeId;
    }

    public string Id
    {
        get => _id ??= _initializeId();
        set => _id = value;
    }

    public string ServiceHostType { get; set; }

    public string FileSystemUserBasePath { get; set; }

    public string FileSystemInstallationPath { get; set; }

    public DateTimeOffset UpSince { get; } = DateTimeOffset.Now;
}
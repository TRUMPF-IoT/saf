// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Contracts;

internal class ServiceHostInfo : IServiceHostInfo
{
    private readonly ServiceHostInfoOptions _options;
    private readonly Func<string> _initializeId;

    private string? _id;

    internal ServiceHostInfo(ServiceHostInfoOptions options, Func<string> initializeId)
    {
        _id = options.Id;
        _options = options;

        _initializeId = initializeId;
    }

    public string Id => _id ??= _initializeId();

    public string ServiceHostType => _options.ServiceHostType;
    public string FileSystemUserBasePath => _options.FileSystemUserBasePath;
    public string FileSystemInstallationPath => _options.FileSystemInstallationPath;

    public DateTimeOffset UpSince { get; } = DateTimeOffset.Now;
}
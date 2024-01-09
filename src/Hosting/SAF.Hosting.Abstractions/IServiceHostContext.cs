// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Abstractions;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides information about the hosting context a plug-in is running in.
/// </summary>
public interface IServiceHostContext
{
    /// <summary>
    /// Gets the applications configuration, to be able to access configuration entries inside a plug-in. 
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>
    /// Gets information about the hosting environment.
    /// </summary>
    IServiceHostEnvironment Environment { get; }

    /// <summary>
    /// Gets information about the Smart Application Framework (SAF) host instance.
    /// </summary>
    IServiceHostInfo HostInfo { get; }
}
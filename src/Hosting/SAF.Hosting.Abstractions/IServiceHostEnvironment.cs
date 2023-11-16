// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Abstractions;

/// <summary>
/// Provides information about the hosting environment a plug-in is running in.
/// </summary>
public interface IServiceHostEnvironment
{
    /// <summary>
    /// Gets or the name of the application. This property is automatically set by the host to the assembly containing
    /// the application entry point.
    /// </summary>
    string? ApplicationName { get; }

    /// <summary>
    /// Gets the name of the environment. The host automatically sets this property to the value of the
    /// "environment" key as specified in configuration. Or uses the value of the environment variables "NETCORE_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT".
    /// It defaults to the "Production" environment.
    /// </summary>
    string EnvironmentName { get; }
}
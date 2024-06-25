// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// An interface for configuring a SAF service host.
/// </summary>
public interface IServiceHostBuilder
{
    /// <summary>
    /// Gets the IServiceCollection where SAF service host services are configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the shared service registry that contains services that will be redirected to the SAF plug-in DI containers.
    /// </summary>
    ISharedServiceRegistry SharedServices { get; }

    /// <summary>
    /// Configures the <see cref="IServiceHostInfo"/> used by the SAF service host.
    /// </summary>
    /// <param name="setupAction">The configuration action.</param>
    /// <returns>The <see cref="IServiceHostBuilder"></see></returns>
    IServiceHostBuilder ConfigureServiceHostInfo(Action<ServiceHostInfoOptions> setupAction);
}
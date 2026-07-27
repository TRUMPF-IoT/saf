// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.DependencyInjection;
using SAF.Common.Diagnostics;
using SAF.PluginSystem.Hosting.Contracts;

internal sealed class SafHostBuilder(IPluginSystemHostBuilder pluginSystemHostBuilder) : ISafHostBuilder
{
    public ISafHostBuilder ConfigureHostInfo(Action<ServiceHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        pluginSystemHostBuilder.Services.Configure(configure);
        return this;
    }

    public ISafHostBuilder ConfigurePluginSystem(Action<IPluginSystemHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(pluginSystemHostBuilder);
        return this;
    }

    public ISafHostBuilder AddHostDiagnostics()
    {
        pluginSystemHostBuilder.Services.AddHostDiagnostics();
        return this;
    }
}

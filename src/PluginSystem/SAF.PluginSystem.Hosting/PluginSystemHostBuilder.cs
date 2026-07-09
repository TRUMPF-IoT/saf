// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <inheritdoc />
public class PluginSystemHostBuilder(IHostApplicationBuilder hostAppBuilder) : IPluginSystemHostBuilder
{
    public IPluginSystemHostEnvironment Environment { get; } = new PluginSystemHostEnvironment(hostAppBuilder.Environment.EnvironmentName, "");

    public IConfigurationManager Configuration { get; } = hostAppBuilder.Configuration;

    public IServiceCollection Services { get; } = hostAppBuilder.Services;
}
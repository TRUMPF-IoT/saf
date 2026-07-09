// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestSequenceRunner;
using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

public class TestAssemblyManifest : IServiceAssemblyManifest, IPluginManifest
{
    public string FriendlyName { get; } = "TestSequenceRunner Test Manifest";

    public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
    {
        // do nothing
    }

    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        // do nothing
    }
}
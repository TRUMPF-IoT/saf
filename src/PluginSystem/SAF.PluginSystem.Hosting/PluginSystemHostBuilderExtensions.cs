// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

public static class PluginSystemHostBuilderExtensions
{
    public static IPluginSystemHostBuilder AddPluginAssemblyFolderContainer(this IPluginSystemHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.AddPluginAssemblyFolderContainer(_ => { });
    }

    public static IPluginSystemHostBuilder AddPluginAssemblyFolderContainer(this IPluginSystemHostBuilder hostBuilder, Action<PluginAssemblyFolderSearchOptions> configureSearch)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var uniqueOptionsKey = $"{nameof(PluginAssemblyFolderSearchOptions)}_{Guid.NewGuid():N}";
        hostBuilder.Services.Configure(uniqueOptionsKey, configureSearch);
        hostBuilder.Services.AddTransient<IPluginAssemblyContainer, PluginAssemblyFolderContainer>(sp =>
        {
            var namedOptionsAccessor = sp.GetRequiredService<IOptionsMonitor<PluginAssemblyFolderSearchOptions>>();
            var options = namedOptionsAccessor.Get(uniqueOptionsKey);

            return new PluginAssemblyFolderContainer(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IPluginManifestLoader>(),
                options,
                sp.GetRequiredService<IFileSystem>());
        });

        return hostBuilder;
    }
}
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SAF.Hosting;
using SAF.PluginSystem.Hosting;

Console.Title = "SAF Message Routing Test Host";

var builder = Host.CreateApplicationBuilder(args);

var pluginAssemblySearchOptions = new PluginAssemblyFolderSearchOptions();
builder.Configuration.GetSection("PluginAssemblies").Bind(pluginAssemblySearchOptions);
pluginAssemblySearchOptions.SearchRootPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, pluginAssemblySearchOptions.SearchRootPath));

builder.AddSafHost()
    .ConfigurePluginSystem(plugins => plugins
        .AddPluginAssemblyFolderContainer(options =>
        {
            options.SearchRootPath = pluginAssemblySearchOptions.SearchRootPath;
            options.Recursive = pluginAssemblySearchOptions.Recursive;
            options.IncludePatterns = pluginAssemblySearchOptions.IncludePatterns;
            options.ExcludePatterns = pluginAssemblySearchOptions.ExcludePatterns;
        }))
    .AddHostDiagnostics();

var host = builder.Build();

await host.RunAsync();
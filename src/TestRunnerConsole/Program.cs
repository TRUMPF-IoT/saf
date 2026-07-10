// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SAF.Common;
using SAF.Messaging.InProcess;
using SAF.PluginSystem.Hosting;

Console.Title = "SAF InProcess Test Host";

var builder = Host.CreateApplicationBuilder(args);

var pluginAssemblySearchOptions = new PluginAssemblyFolderSearchOptions();
builder.Configuration.GetSection("PluginAssemblies").Bind(pluginAssemblySearchOptions);
pluginAssemblySearchOptions.SearchRootPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, pluginAssemblySearchOptions.SearchRootPath));

builder.AddPluginSystem(_ => { })
    .AddPluginAssemblyFolderContainer(options =>
    {
        options.SearchRootPath = pluginAssemblySearchOptions.SearchRootPath;
        options.Recursive = pluginAssemblySearchOptions.Recursive;
        options.IncludePatterns = pluginAssemblySearchOptions.IncludePatterns;
        options.ExcludePatterns = pluginAssemblySearchOptions.ExcludePatterns;
    });

builder.Services.AddInProcessMessagingInfrastructure()
    .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IInProcessMessagingInfrastructure>());

var host = builder.Build();

await host.RunAsync();
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

public class PluginAssemblyFolderSearchOptions
{
    public string SearchRootPath { get; set; } = AppContext.BaseDirectory;
    public bool Recursive { get; set; } = false;
    public string IncludePatterns { get; set; } = "*.dll";
    public string ExcludePatterns { get; set; } = "Microsoft.*;System.*;Serilog.*;SAF.PluginSystem.*";
}
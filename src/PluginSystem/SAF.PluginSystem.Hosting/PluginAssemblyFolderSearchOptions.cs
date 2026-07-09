// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

public class PluginAssemblyFolderSearchOptions
{
    public string SearchRootPath { get; set; } = AppContext.BaseDirectory;
    public bool Recursive { get; set; } = false;
    public string IncludePatterns { get; set; } = "*.dll";
    public string ExcludePatterns { get; set; } = "Microsoft.*;System.*;Serilog.*;SAF.PluginSystem.*";
}
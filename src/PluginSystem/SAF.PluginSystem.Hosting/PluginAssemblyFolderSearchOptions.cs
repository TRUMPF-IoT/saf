// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

public class PluginAssemblyFolderSearchOptions
{
    public string SearchRootPath { get; set; } = AppContext.BaseDirectory;
    public bool Recursive { get; set; } = false;
    /// <summary>
    /// Semicolon-separated glob patterns of the plug-in assemblies to load.
    /// </summary>
    /// <remarks>
    /// The assemblies load in the order of the patterns, and the ones matching the same pattern in the ordinal order
    /// of their full paths. An assembly matching several patterns loads once, at its first one.
    /// </remarks>
    public string IncludePatterns { get; set; } = "*.dll";
    public string ExcludePatterns { get; set; } = "Microsoft.*;System.*;Serilog.*;SAF.PluginSystem.*";
}
// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Reflection;
using SAF.PluginSystem.Hosting.Contracts;

public class PublicServiceTypeRegistry(
    ILogger<PublicServiceTypeRegistry> logger,
    IOptions<PluginSystemOptions> options)
    : IPublicServiceTypeRegistry
{
    private readonly PluginSystemOptions _options = options.Value;
    private readonly List<string> _assemblies = [];
    private readonly Lock _syncAssemblies = new();
    private bool _initialized = false;

    public IEnumerable<string> GetAssemblyNames()
    {
        SearchPublicServiceTypeAssemblies();
        return _assemblies;
    }

    private void SearchPublicServiceTypeAssemblies()
    {
        lock (_syncAssemblies)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (string.IsNullOrWhiteSpace(_options.PluginContractsSearchPattern))
            {
                logger.LogInformation("No search pattern for plug-in contract assemblies specified. No public plug-in services will be available.");
                return;
            }

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddIncludePatterns(_options.PluginContractsSearchPattern.Split(';'));

            var result = matcher.GetResultsInFullPath(AppContext.BaseDirectory).ToList();
            logger.LogDebug("Found {MatchingAssemblyCount} matching plug-in contract assemblies", result.Count);

            _assemblies.AddRange(result.Select(a => AssemblyName.GetAssemblyName(a).FullName));
        }
    }
}
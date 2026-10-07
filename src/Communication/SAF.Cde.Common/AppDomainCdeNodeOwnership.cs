// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;
using System.Runtime.Loader;

/// <summary>
/// Records the copy of SAF.Cde.Common that runs C-DEngine in the AppDomain data of the process.
/// </summary>
/// <remarks>
/// AppDomain data is one store for the whole process, across all assembly load contexts, so a second copy
/// of SAF.Cde.Common in another context sees the marker. Each copy also has its own copy of C-DEngine and would
/// start a second node next to the first. The check is a diagnostic, not a lock: two copies claiming at
/// the very same moment can both pass it.
/// </remarks>
internal sealed class AppDomainCdeNodeOwnership(string markerKey) : ICdeNodeOwnership
{
    internal const string DefaultMarkerKey = "SAF.Cde.Common.CdeNode.Owner";

    private readonly string _owner = DescribeOwner();

    public void Claim()
    {
        var domain = AppDomain.CurrentDomain;
        if (domain.GetData(markerKey) is string owner && owner != _owner)
        {
            throw new InvalidOperationException(
                $"C-DEngine is already running in this process, started by another copy of SAF.Cde.Common ({owner}). " +
                $"This copy ({_owner}) cannot start a second node. Load every plug-in that uses C-DEngine, such as " +
                "SAF.Messaging.Cde and SAF.Storage.Cde, from the host's base directory (AppContext.BaseDirectory), preferably through a " +
                "PackageReference in the host. A shared plug-in folder outside the base directory is not enough: " +
                "the plugin system loads every plug-in assembly there into its own AssemblyLoadContext, each with its " +
                "own copy of SAF.Cde.Common and C-DEngine.");
        }

        domain.SetData(markerKey, _owner);
    }

    private static string DescribeOwner()
    {
        var assembly = typeof(AppDomainCdeNodeOwnership).Assembly;
        var loadContext = AssemblyLoadContext.GetLoadContext(assembly)?.Name ?? "unnamed";
        return $"{assembly.Location} in AssemblyLoadContext '{loadContext}', instance {Guid.NewGuid():N}";
    }
}
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <inheritdoc />
internal sealed class SharedAssemblyVersionComparer : ISharedAssemblyVersionComparer
{
    private static readonly Version LowestVersion = new(0, 0, 0, 0);

    /// <inheritdoc />
    public SharedAssemblyVersionRelation Compare(Version hostVersion, Version? requestedVersion)
    {
        ArgumentNullException.ThrowIfNull(hostVersion);

        var requested = requestedVersion ?? LowestVersion;

        var comparison = hostVersion.CompareTo(requested);
        return comparison switch
        {
            > 0 => SharedAssemblyVersionRelation.Higher,
            < 0 => SharedAssemblyVersionRelation.Lower,
            _ => SharedAssemblyVersionRelation.Equal
        };
    }
}

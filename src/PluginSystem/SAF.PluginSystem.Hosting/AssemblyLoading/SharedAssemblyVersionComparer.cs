// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <inheritdoc />
internal sealed class SharedAssemblyVersionComparer : ISharedAssemblyVersionComparer
{
    private static readonly Version LowestVersion = new(0, 0, 0, 0);

    /// <inheritdoc />
    public SharedAssemblyVersionRelation Compare(Version hostVersion, Version? requestedVersion)
    {
        ArgumentNullException.ThrowIfNull(hostVersion);

        var requested = requestedVersion ?? LowestVersion;

        var comparison = Normalize(hostVersion).CompareTo(Normalize(requested));
        return comparison switch
        {
            > 0 => SharedAssemblyVersionRelation.Higher,
            < 0 => SharedAssemblyVersionRelation.Lower,
            _ => SharedAssemblyVersionRelation.Equal
        };
    }

    // Version.CompareTo treats an unspecified Build/Revision (-1) as lower than any specified one, so
    // "1.0" and "1.0.0.0" compare as different versions even though a plugin's AssemblyRef (always
    // four-field) and a hand-written ISharedAssemblySource's Version (e.g. new Version(1, 0)) mean the same
    // version. Normalizing both sides first makes the comparison field-count-independent.
    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}

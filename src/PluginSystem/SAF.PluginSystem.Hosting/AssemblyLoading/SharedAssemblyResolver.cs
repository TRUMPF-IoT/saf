// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Options;
using System.Reflection;

/// <inheritdoc />
internal sealed class SharedAssemblyResolver(
    ISharedAssemblyRegistry sharedAssemblyRegistry,
    IOptions<PluginSystemOptions> options)
    : ISharedAssemblyResolver
{
    private static readonly Version LowestVersion = new(0, 0, 0, 0);

    /// <inheritdoc />
    public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? hostVersion)
    {
        ArgumentNullException.ThrowIfNull(requested);

        hostVersion = null;

        if (requested.Name is null || !sharedAssemblyRegistry.TryGetSharedAssembly(requested.Name, out var info))
        {
            return SharedAssemblyDecision.LoadIsolated;
        }

        // A shared assembly is identified by simple name and public key token (the version is negotiated
        // below). A differing token means a genuinely different assembly that must not be shared. Culture
        // needs no explicit check here: only culture-neutral contract assemblies enter the registry, and
        // culture-specific satellite assemblies carry a distinct ".resources" simple name, so they never
        // match a registered entry in the first place.
        if (!PublicKeyTokensMatch(requested.GetPublicKeyToken(), info.PublicKeyToken))
        {
            return SharedAssemblyDecision.LoadIsolated;
        }

        hostVersion = info.Version;

        if (IsLowerThanRequested(info.Version, requested.Version) || IsBreakingMajorRollForward(info.Version, requested.Version))
        {
            return SharedAssemblyDecision.Conflict;
        }

        return SharedAssemblyDecision.ShareFromDefault;
    }

    private bool IsBreakingMajorRollForward(Version hostVersion, Version? requestedVersion)
        => !options.Value.AllowMajorVersionRollForward && requestedVersion is not null && hostVersion.Major > requestedVersion.Major;

    // Version.CompareTo treats an unspecified Build/Revision (-1) as lower than any specified one, so
    // "1.0" and "1.0.0.0" compare as different versions even though a plugin's AssemblyRef (always
    // four-field) and a hand-written ISharedAssemblySource's Version (e.g. new Version(1, 0)) mean the same
    // version. Normalizing both sides first makes the comparison field-count-independent.
    private static bool IsLowerThanRequested(Version hostVersion, Version? requestedVersion)
        => Normalize(hostVersion).CompareTo(Normalize(requestedVersion ?? LowestVersion)) < 0;

    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static bool PublicKeyTokensMatch(byte[]? requestedToken, string? hostToken)
        => SharedAssemblyInfo.NormalizeToken(requestedToken) == hostToken;
}

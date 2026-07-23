// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <inheritdoc />
internal sealed class SharedAssemblyResolver(
    ISharedAssemblyRegistry sharedAssemblyRegistry,
    ISharedAssemblyVersionComparer versionComparer)
    : ISharedAssemblyResolver
{
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

        return versionComparer.Compare(info.Version, requested.Version) switch
        {
            SharedAssemblyVersionRelation.Lower => SharedAssemblyDecision.Conflict,
            _ => SharedAssemblyDecision.ShareFromDefault
        };
    }

    private static bool PublicKeyTokensMatch(byte[]? requested, byte[]? host)
    {
        var requestedEmpty = requested is null || requested.Length == 0;
        var hostEmpty = host is null || host.Length == 0;

        if (requestedEmpty || hostEmpty)
        {
            return requestedEmpty && hostEmpty;
        }

        return requested.AsSpan().SequenceEqual(host);
    }
}

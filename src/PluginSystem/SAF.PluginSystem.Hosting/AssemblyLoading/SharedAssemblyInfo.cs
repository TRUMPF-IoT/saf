// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Describes a shared assembly as provided by the host: the version the host offers and its public key
/// token (used to distinguish otherwise identically named assemblies).
/// </summary>
/// <param name="Version">The version the host provides for this assembly.</param>
/// <param name="PublicKeyToken">
/// The lowercase hex-encoded public key token of the host assembly, or <see langword="null"/> if it is not
/// strong-named. Stored as a string, not the raw bytes reflection returns, so this stays a proper value
/// type: a <c>byte[]</c> field would make the record's generated <c>Equals</c>/<c>GetHashCode</c> compare by
/// reference instead of by content.
/// </param>
internal readonly record struct SharedAssemblyInfo(Version Version, string? PublicKeyToken)
{
    /// <summary>
    /// Converts a raw public key token to the lowercase hex form <see cref="PublicKeyToken"/> stores.
    /// </summary>
    public static string? NormalizeToken(byte[]? token)
        => token is { Length: > 0 } ? Convert.ToHexString(token).ToLowerInvariant() : null;
}

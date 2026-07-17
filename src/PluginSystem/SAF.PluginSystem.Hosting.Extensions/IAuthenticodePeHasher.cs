// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using System.Security.Cryptography.Pkcs;

/// <summary>
/// Verifies that an Authenticode signature actually covers the contents of a PE file by comparing
/// the digest embedded in the signature to a recomputed Authenticode hash of the file.
/// </summary>
internal interface IAuthenticodePeHasher
{
    /// <summary>
    /// Returns <see langword="true"/> when the digest embedded in <paramref name="signedCms"/>
    /// matches the recomputed Authenticode hash of the file at <paramref name="assemblyPath"/>.
    /// </summary>
    bool VerifyEmbeddedHashMatchesFile(string assemblyPath, SignedCms signedCms);
}

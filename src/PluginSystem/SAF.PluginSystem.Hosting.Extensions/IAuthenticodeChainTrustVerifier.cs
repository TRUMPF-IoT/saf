// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Determines whether a code-signing certificate (and, where the platform supports it, the whole
/// signed file) is trusted according to the platform's Authenticode trust policy.
/// </summary>
internal interface IAuthenticodeChainTrustVerifier
{
    bool IsTrusted(string assemblyPath, X509Certificate2 signerCertificate);
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

internal interface IAuthenticodeSignatureReader
{
    bool TryGetSignatureInfo(string assemblyPath, out string? signerThumbprint, out bool hasValidDigitalSignature);
}

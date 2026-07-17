// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

public sealed class DigitalSignaturePluginAssemblyValidatorOptions
{
    public bool RequireValidDigitalSignature { get; set; }

    public ISet<string> AllowedSignerThumbprints { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

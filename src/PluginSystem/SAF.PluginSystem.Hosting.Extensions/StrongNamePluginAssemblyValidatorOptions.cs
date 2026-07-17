// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

public sealed class StrongNamePluginAssemblyValidatorOptions
{
    public bool RequireStrongName { get; set; }

    public ISet<string> AllowedPublicKeyTokens { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

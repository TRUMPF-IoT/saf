// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <summary>
/// Represents the outcome of a plugin assembly validation step.
/// </summary>
public readonly record struct PluginAssemblyValidationResult(bool IsAccepted, string? Reason)
{
    public static PluginAssemblyValidationResult Accepted() => new(true, null);

    public static PluginAssemblyValidationResult Rejected(string reason) => new(false, reason);
}

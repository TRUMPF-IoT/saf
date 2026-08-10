// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Represents the outcome of a plugin assembly validation step.
/// </summary>
public readonly record struct PluginAssemblyValidationResult
{
    /// <summary>
    /// Initializes a validation result.
    /// </summary>
    /// <param name="isAccepted"><see langword="true"/> when the assembly may be loaded.</param>
    /// <param name="reason">The reason for rejection, or <see langword="null"/> when accepted.</param>
    public PluginAssemblyValidationResult(bool isAccepted, string? reason)
    {
        IsAccepted = isAccepted;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the assembly passed validation.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// Gets the reason for rejection, or <see langword="null"/> when the assembly was accepted.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates an accepted validation result.
    /// </summary>
    public static PluginAssemblyValidationResult Accepted() => new(true, null);

    /// <summary>
    /// Creates a rejected validation result.
    /// </summary>
    /// <param name="reason">The reason the assembly was rejected.</param>
    public static PluginAssemblyValidationResult Rejected(string reason) => new(false, reason);
}

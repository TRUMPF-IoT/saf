// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <summary>
/// Validates a plugin assembly candidate before it is loaded.
/// </summary>
public interface IPluginAssemblyValidator
{
    /// <summary>
    /// Validates the provided assembly candidate.
    /// </summary>
    /// <param name="context">Validation context containing assembly metadata.</param>
    /// <returns>A validation result that decides whether loading is allowed.</returns>
    PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context);
}

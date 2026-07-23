// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Describes how the version provided by the host relates to the version requested by a plugin for a
/// shared assembly.
/// </summary>
internal enum SharedAssemblyVersionRelation
{
    /// <summary>The host provides a higher version than requested (backward-compatible roll-forward).</summary>
    Higher,

    /// <summary>The host provides exactly the requested version.</summary>
    Equal,

    /// <summary>The host provides a lower version than requested (incompatible).</summary>
    Lower
}

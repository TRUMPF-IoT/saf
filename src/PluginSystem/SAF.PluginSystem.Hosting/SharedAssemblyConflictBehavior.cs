// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <summary>
/// Controls how the plugin system reacts when a plugin requests a higher version of a shared
/// (contract) assembly than the host provides.
/// </summary>
public enum SharedAssemblyConflictBehavior
{
    /// <summary>
    /// Fail fast: throw a <see cref="SharedAssemblyVersionConflictException"/> so the misconfiguration
    /// surfaces immediately with a clear diagnostic instead of as an obscure runtime type error.
    /// This is the default.
    /// </summary>
    Fail,

    /// <summary>
    /// Best effort: load the requested version privately into the isolated plugin context and log a
    /// warning. The plugin may start, but types of this shared assembly can no longer cross the
    /// plugin boundary safely.
    /// </summary>
    IsolateWithWarning
}

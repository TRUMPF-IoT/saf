// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Thrown when a plugin requests a version of a shared (contract) assembly that is not compatible with
/// the one the host provides (the host is older, or newer across a breaking major version) and
/// <see cref="PluginSystemOptions.SharedAssemblyConflictBehavior"/> is set to
/// <see cref="SharedAssemblyConflictBehavior.Fail"/>.
/// </summary>
public sealed class SharedAssemblyVersionConflictException : Exception
{
    /// <summary>Gets the simple name of the conflicting shared assembly.</summary>
    public string SharedAssemblyName { get; }

    /// <summary>Gets the version requested by the plugin.</summary>
    public Version RequestedVersion { get; }

    /// <summary>Gets the (lower) version provided by the host.</summary>
    public Version HostVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedAssemblyVersionConflictException"/> class.
    /// </summary>
    public SharedAssemblyVersionConflictException(string sharedAssemblyName, Version requestedVersion, Version hostVersion)
        : base($"A plugin requires shared assembly '{sharedAssemblyName}' version {requestedVersion}, which is not " +
               $"compatible with the host-provided version {hostVersion}. Types of this shared (contract) assembly " +
               $"cannot cross the plugin boundary. Deploy a compatible host version, or exclude the assembly from " +
               $"the shared set so the plugin can load it in isolation.")
    {
        SharedAssemblyName = sharedAssemblyName;
        RequestedVersion = requestedVersion;
        HostVersion = hostVersion;
    }
}

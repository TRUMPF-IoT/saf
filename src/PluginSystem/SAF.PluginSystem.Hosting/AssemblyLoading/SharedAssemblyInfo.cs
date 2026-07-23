// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Describes a shared assembly as provided by the host: the version the host offers and its public key
/// token (used to distinguish otherwise identically named assemblies).
/// </summary>
/// <param name="Version">The version the host provides for this assembly.</param>
/// <param name="PublicKeyToken">The public key token of the host assembly, or <see langword="null"/> if it is not strong-named.</param>
internal readonly record struct SharedAssemblyInfo(Version Version, byte[]? PublicKeyToken);

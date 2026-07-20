// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds the set of secret store providers for a registration. Providers are registered in
/// priority order: with <see cref="SecretStoreOptions.AutoProviderName"/> the first available
/// provider (in the order they were added here) is selected.
/// </summary>
public interface ISecretStoreBuilder
{
    /// <summary>The underlying service collection the providers are registered in.</summary>
    IServiceCollection Services { get; }
}

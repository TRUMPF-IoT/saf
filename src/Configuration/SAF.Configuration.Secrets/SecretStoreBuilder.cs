// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.DependencyInjection;

/// <inheritdoc />
internal sealed class SecretStoreBuilder(IServiceCollection services) : ISecretStoreBuilder
{
    public IServiceCollection Services { get; } = services;
}

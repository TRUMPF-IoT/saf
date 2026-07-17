// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Configuration;

internal sealed class PluginConfigurationSourcesOptions
{
    public IList<Action<IConfigurationBuilder>> ConfigureSources { get; } = [];
}

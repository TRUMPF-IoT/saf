// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Contracts;

/// <inheritdoc />
public class PluginSystemHostEnvironment(string environmentName, string pluginSettingsRootPath) : IPluginSystemHostEnvironment
{
    public string EnvironmentName { get; } = environmentName;

    public string PluginSettingsRootPath { get; set; } = pluginSettingsRootPath;
}
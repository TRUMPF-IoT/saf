// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Abstractions;

internal class ServiceHostEnvironment : IServiceHostEnvironment
{
    public string? ApplicationName { get; set; }
    public string EnvironmentName { get; set; } = default!;
}
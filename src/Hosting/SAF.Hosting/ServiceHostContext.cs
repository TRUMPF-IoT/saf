// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class ServiceHostContext : IServiceHostContext
{
    public IConfiguration Configuration { get; set; } = default!;
    public IServiceHostEnvironment Environment { get; set; } = default!;
    public IServiceHostInfo HostInfo { get; set; } = default!;
}
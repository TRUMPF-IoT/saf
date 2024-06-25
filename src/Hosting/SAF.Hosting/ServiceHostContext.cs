// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.Configuration;
using Contracts;

internal class ServiceHostContext : IServiceHostContext
{
    public IConfiguration Configuration { get; set; } = default!;
    public IServiceHostEnvironment Environment { get; set; } = default!;
    public IServiceHostInfo HostInfo { get; set; } = default!;
}
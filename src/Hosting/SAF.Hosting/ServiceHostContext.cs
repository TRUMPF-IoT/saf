﻿// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using SAF.Common;

namespace SAF.Hosting;

internal class ServiceHostContext : IServiceHostContext
{
    public IConfiguration Configuration { get; set; } = default!;
    public IServiceHostEnvironment Environment { get; set; } = default!;
    public IHostInfo HostInfo { get; set; } = default!;
}
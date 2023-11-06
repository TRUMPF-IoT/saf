// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;

namespace SAF.Common;

public interface IServiceHostContext
{
    IConfiguration Configuration { get; }
    IServiceHostEnvironment Environment { get; }
    IHostInfo HostInfo { get; }
}
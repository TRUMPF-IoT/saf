// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Contracts;

internal class SharedServiceRegistry : ISharedServiceRegistry
{
    public IServiceCollection Services { get; } = new ServiceCollection();
}
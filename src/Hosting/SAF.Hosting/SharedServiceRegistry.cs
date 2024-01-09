// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Abstractions;

internal class SharedServiceRegistry : ISharedServiceRegistry
{
    internal IServiceCollection SharedServices { get; } = new ServiceCollection();

    public IEnumerable<ServiceDescriptor> Services => SharedServices;
}
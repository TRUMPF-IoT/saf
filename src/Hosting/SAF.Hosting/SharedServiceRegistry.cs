// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Contracts;

internal class SharedServiceRegistry : ISharedServiceRegistry
{
    public IServiceCollection Services { get; } = new ServiceCollection();
}
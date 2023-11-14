// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

namespace TestSequenceRunner;

public class TestAssemblyManifest : IServiceAssemblyManifest
{
    public string FriendlyName { get; } = "TestSequenceRunner Test Manifest";

    public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
    {
        // do nothing
    }
}
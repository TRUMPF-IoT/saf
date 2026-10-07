// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCde_RegistersTheConfiguredConfiguration()
    {
        var services = new ServiceCollection();

        services.AddCde(config => config.ScopeId = "4711");

        using var provider = services.BuildServiceProvider();
        Assert.Equal("4711", provider.GetRequiredService<CdeConfiguration>().ScopeId);
    }

    [Fact]
    public void AddCde_UsesTheSharedNode_WhenNoNodeIsRegistered()
    {
        var services = new ServiceCollection();

        services.AddCde(_ => { });

        // Resolving the lease from the shared node would start C-DEngine, so only the registration is checked.
        var node = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICdeNode));
        Assert.Same(CdeNode.Shared, node.ImplementationInstance);
    }

    [Fact]
    public void AddCde_AcquiresTheLeaseWithTheConfiguration_WhenTheLeaseIsResolved()
    {
        var node = new FakeNode();
        var services = new ServiceCollection().AddSingleton<ICdeNode>(node);

        services.AddCde(config => config.ScopeId = "4711");

        using (var provider = services.BuildServiceProvider())
        {
            _ = provider.GetRequiredService<CdeNodeLease>();

            Assert.Equal("4711", node.AcquiredWith?.ScopeId);
            Assert.Equal(0, node.ReleaseCount);
        }

        Assert.Equal(1, node.ReleaseCount);
    }

    private sealed class FakeNode : ICdeNode
    {
        public CdeConfiguration? AcquiredWith { get; private set; }
        public int ReleaseCount { get; private set; }

        public CdeNodeLease Acquire(CdeConfiguration configuration, ILoggerFactory loggerFactory)
        {
            AcquiredWith = configuration;
            return new CdeNodeLease(() => ReleaseCount++);
        }
    }
}
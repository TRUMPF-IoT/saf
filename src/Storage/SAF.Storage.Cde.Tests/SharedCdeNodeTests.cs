// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.Cde.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nsCDEngine.BaseClasses;
using NSubstitute;
using SAF.Cde;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;
using MessagingPluginManifest = SAF.Messaging.Cde.PluginManifest;
using StoragePluginManifest = SAF.Storage.Cde.PluginManifest;

// Loads both C-DEngine plug-ins into containers of their own, as the plugin system does when both come from the
// host's base directory. The CdeFixture started the node, so each container only adds a lease to it.
[Collection(CdeCollection.Name)]
public class SharedCdeNodeTests(CdeFixture cde)
{
    private const string Area = "sharedcdenode";

    private static int LeaseCount => ((CdeNode)CdeNode.Shared).LeaseCount;

    [Fact]
    public void LoadingBothPlugins_RunsThemOnTheOneNode()
    {
        var leasesBefore = LeaseCount;

        using var storagePlugin = LoadPlugin(new StoragePluginManifest());
        using var messagingPlugin = LoadPlugin(new MessagingPluginManifest());
        var storage = storagePlugin.GetRequiredService<IStorageInfrastructure>();
        var messaging = CreateMessaging(messagingPlugin);

        storage.Set(Area, "loaded", "value");
        Assert.Equal("value", storage.GetString(Area, "loaded"));
        Assert.NotNull(messaging);
        Assert.Equal(leasesBefore + 2, LeaseCount);
    }

    [Fact]
    public void DisposingTheStoragePluginFirst_KeepsTheNodeForMessaging()
    {
        var leasesBefore = LeaseCount;
        using var storagePlugin = LoadPlugin(new StoragePluginManifest());
        using var messagingPlugin = LoadPlugin(new MessagingPluginManifest());
        storagePlugin.GetRequiredService<IStorageInfrastructure>().Set(Area, "storageFirst", "value");
        var messaging = CreateMessaging(messagingPlugin);

        storagePlugin.Dispose();

        Assert.Equal(leasesBefore + 1, LeaseCount);
        Assert.True(TheBaseAssets.MasterSwitch);
        messaging.Publish(new Message { Topic = "private/saf/storage/cde/tests" });

        messagingPlugin.Dispose();

        Assert.Equal(leasesBefore, LeaseCount);
    }

    [Fact]
    public void DisposingTheMessagingPluginFirst_KeepsTheNodeForTheStorage()
    {
        var leasesBefore = LeaseCount;
        using var storagePlugin = LoadPlugin(new StoragePluginManifest());
        using var messagingPlugin = LoadPlugin(new MessagingPluginManifest());
        var storage = storagePlugin.GetRequiredService<IStorageInfrastructure>();
        CreateMessaging(messagingPlugin);

        messagingPlugin.Dispose();

        Assert.Equal(leasesBefore + 1, LeaseCount);
        Assert.True(TheBaseAssets.MasterSwitch);
        storage.Set(Area, "messagingFirst", "value");
        Assert.Equal("value", storage.GetString(Area, "messagingFirst"));

        storagePlugin.Dispose();

        Assert.Equal(leasesBefore, LeaseCount);
    }

    private ServiceProvider LoadPlugin(IPluginManifest manifest)
    {
        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(cde.HostConfiguration);
        context.PluginConfiguration.Returns(new ConfigurationBuilder().Build());

        // The plugin system imports the dispatcher of SAF.Messaging.Runtime into every container.
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IServiceMessageDispatcher>());
        manifest.ConfigureServices(context, services);
        return services.BuildServiceProvider();
    }

    private static IMessagingInfrastructure CreateMessaging(IServiceProvider messagingPlugin)
        => messagingPlugin.GetRequiredKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde)
            .Create(new MessagingConfiguration { Key = MessagingInfrastructureKeys.Cde });
}
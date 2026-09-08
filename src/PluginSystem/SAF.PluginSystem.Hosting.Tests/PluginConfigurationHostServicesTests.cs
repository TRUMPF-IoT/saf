// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;

public class PluginConfigurationHostServicesTests
{
    [Fact]
    public void GetService_ForwardsToTheHostContainer()
    {
        var hostServices = new PluginConfigurationHostServices(new DelegateServiceProvider(_ => "value"));

        Assert.Equal("value", hostServices.GetService(typeof(string)));
    }

    [Fact]
    public void GetService_ForIServiceProvider_ReturnsItself()
    {
        var hostServices = new PluginConfigurationHostServices(
            new DelegateServiceProvider(_ => throw new InvalidOperationException("must not be forwarded")));

        Assert.Same(hostServices, hostServices.GetService(typeof(IServiceProvider)));
    }

    [Fact]
    public void GetService_Throws_OnNullServiceType()
    {
        var hostServices = new PluginConfigurationHostServices(new DelegateServiceProvider(_ => null));

        Assert.Throws<ArgumentNullException>(() => hostServices.GetService(null!));
    }

    [Fact]
    public void IsResolving_IsTrueOnlyWhileTheHostContainerIsAnswering()
    {
        PluginConfigurationHostServices? hostServices = null;
        var observedWhileResolving = false;
        hostServices = new PluginConfigurationHostServices(new DelegateServiceProvider(_ =>
        {
            observedWhileResolving = hostServices!.IsResolving;
            return null;
        }));

        Assert.False(hostServices.IsResolving);
        hostServices.GetService(typeof(string));

        Assert.True(observedWhileResolving);
        Assert.False(hostServices.IsResolving);
    }

    [Fact]
    public void IsResolving_StaysTrue_AfterANestedResolutionCompletes()
    {
        // Restoring the previous type rather than clearing it is what keeps the guard armed for the rest of
        // the outer resolution - the container resolves the whole dependency graph inside that one call.
        PluginConfigurationHostServices? hostServices = null;
        var observedAfterNested = false;
        var nested = false;
        hostServices = new PluginConfigurationHostServices(new DelegateServiceProvider(_ =>
        {
            if (!nested)
            {
                nested = true;
                hostServices!.GetService(typeof(int));
                observedAfterNested = hostServices.IsResolving;
            }

            return null;
        }));

        hostServices.GetService(typeof(string));

        Assert.True(observedAfterNested);
    }

    [Fact]
    public void IsResolving_IsReset_WhenTheHostContainerThrows()
    {
        var hostServices = new PluginConfigurationHostServices(
            new DelegateServiceProvider(_ => throw new InvalidOperationException("boom")));

        Assert.Throws<InvalidOperationException>(() => hostServices.GetService(typeof(string)));
        Assert.False(hostServices.IsResolving);
    }

    [Fact]
    public void CircularResolution_NamesTheServiceTheCallbackAskedFor()
    {
        PluginConfigurationHostServices? hostServices = null;
        InvalidOperationException? exception = null;
        hostServices = new PluginConfigurationHostServices(new DelegateServiceProvider(_ =>
        {
            exception = hostServices!.CircularResolution();
            return null;
        }));

        hostServices.GetService(typeof(IPluginServiceProvider));

        Assert.NotNull(exception);
        Assert.Contains(typeof(IPluginServiceProvider).ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IPluginSystemHostContext), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PluginConfigurationSourceContext.HostServices), exception.Message, StringComparison.Ordinal);
    }

    private sealed class DelegateServiceProvider(Func<Type, object?> getService) : IServiceProvider
    {
        public object? GetService(Type serviceType) => getService(serviceType);
    }
}

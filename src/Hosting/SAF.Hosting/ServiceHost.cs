// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Common;
using Contracts;

/// <summary>
/// Central entry point for initializing and starting the services used.
/// </summary>
internal class ServiceHost(
    ILogger<ServiceHost> logger,
    IServiceProvider applicationServiceProvider,
    IServiceMessageHandlerTypes hostMessageHandlerTypes,
    ISharedServiceRegistry sharedServiceRegistry,
    IServiceAssemblyManager serviceAssemblyManager,
    IServiceMessageDispatcher messageDispatcher,
    IConfiguration configuration)
    : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly List<ServiceProvider> _serviceAssemblyServiceProviders = [];
    private readonly List<HostedServiceContainer> _services = [];

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await StartInternalAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // intentionally ignored
            }
            catch (OperationCanceledException)
            {
                // intentionally ignored
            }
            finally
            {
                linkedCts.Dispose();
            }
        }, cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_services.Count == 0)
            return;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await StopServicesAsync(_services, linkedCts.Token);

            stopWatch.Stop();
            logger.LogInformation("Stopping all services took {ServiceShutdownTime:N0} ns",
                stopWatch.Elapsed.TotalMilliseconds * 1000000);

            _services.Clear();
            _serviceAssemblyServiceProviders.ForEach(sp => sp.Dispose());
            _serviceAssemblyServiceProviders.Clear();
        }
        catch (TaskCanceledException)
        {
            // intentionally ignored
        }
        catch (OperationCanceledException)
        {
            // intentionally ignored
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    private async Task StartInternalAsync(CancellationToken token)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        InitializeServices();
        AddApplicationMessageHandlersToDispatcher();

        await StartServicesAsync(_services, token);

        stopWatch.Stop();

        logger.LogInformation("Starting all services took {ServiceStartUpTime:N0} ns",
            stopWatch.Elapsed.TotalMilliseconds * 1000000);
    }

    private async Task StartServicesAsync(IEnumerable<HostedServiceContainer> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            var asyncServiceStarts = new List<Task>();
            foreach (var service in services.TakeWhile(_ => !linkedCts.Token.IsCancellationRequested))
            {
                logger.LogDebug("Starting service: {ServiceName}", service.GetType().Name);

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                if (service.AsyncService is not null)
                {
                    asyncServiceStarts.Add(service.AsyncService.StartAsync(linkedCts.Token)
                        .ContinueWith(_ =>
                        {
                            serviceStopWatch.Stop();
                            LogServiceStartupTime(service, serviceStopWatch);

                        }, linkedCts.Token));
                }
                else
                {
                    service.Service?.Start();

                    serviceStopWatch.Stop();
                    LogServiceStartupTime(service, serviceStopWatch);
                }
            }

            await Task.WhenAll(asyncServiceStarts);
        }
        catch (TaskCanceledException)
        {
            // intentionally ignored
        }
        catch (OperationCanceledException)
        {
            // intentionally ignored
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    private async Task StopServicesAsync(IEnumerable<HostedServiceContainer> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            var asyncServiceStops = new List<Task>();
            foreach (var service in services.TakeWhile(_ => !cancelToken.IsCancellationRequested))
            {
                logger.LogDebug("Stopping service: {ServiceName}", service.GetType().Name);

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                if (service.AsyncService is not null)
                {
                    asyncServiceStops.Add(service.AsyncService.StopAsync(linkedCts.Token)
                        .ContinueWith(_ =>
                        {
                            serviceStopWatch.Stop();
                            LogServiceShutdownTime(service, serviceStopWatch);

                        }, linkedCts.Token));
                }
                else
                {
                    service.Service?.Stop();

                    serviceStopWatch.Stop();
                    LogServiceShutdownTime(service, serviceStopWatch);
                }
            }

            await Task.WhenAll(asyncServiceStops);
        }
        catch (TaskCanceledException)
        {
            // intentionally ignored
        }
        catch (OperationCanceledException)
        {
            // intentionally ignored
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    private void LogServiceStartupTime(HostedServiceContainer service, Stopwatch stopWatch)
        => logger.LogInformation(
            "Started service: {ServiceName}, start-up took {ServiceStartUpTime:N0} ns",
            service.AsyncService?.GetType().Name ?? service.Service?.GetType().Name,
            stopWatch.Elapsed.TotalMilliseconds * 1000000);

    private void LogServiceShutdownTime(HostedServiceContainer service, Stopwatch stopWatch)
        => logger.LogInformation(
            "Stopped service: {ServiceName}, shutdown took {ServiceStartUpTime:N0} ns",
            service.AsyncService?.GetType().Name ?? service.Service?.GetType().Name,
            stopWatch.Elapsed.TotalMilliseconds * 1000000);

    private void InitializeServices()
    {
        var context = BuildServiceHostContext();

        foreach(var manifest in serviceAssemblyManager.GetServiceAssemblyManifests())
        {
            logger.LogInformation("Initializing service assembly: {ServiceName}.", manifest.FriendlyName);

            var assemblyServiceCollection = new ServiceCollection();

            RedirectCommonServices(assemblyServiceCollection, context);
            sharedServiceRegistry.RedirectServices(applicationServiceProvider, assemblyServiceCollection);

            manifest.RegisterDependencies(assemblyServiceCollection, context);

            var assemblyServiceProvider = assemblyServiceCollection.BuildServiceProvider();
            _serviceAssemblyServiceProviders.Add(assemblyServiceProvider);

#pragma warning disable CS0618 // Type or member is obsolete
            var servicesToAdd = assemblyServiceProvider.GetServices<IHostedService>();
#pragma warning restore CS0618 // Type or member is obsolete
            var asyncServicesToAdd = assemblyServiceProvider.GetServices<IHostedServiceAsync>();
            _services.AddRange(servicesToAdd.Select(s => new HostedServiceContainer(null, s)));
            _services.AddRange(asyncServicesToAdd.Select(s => new HostedServiceContainer(s, null)));

            var assemblyMessageHandlerTypes = new ServiceMessageHandlerTypes(assemblyServiceCollection);
            AddMessageHandlersToDispatcher(assemblyMessageHandlerTypes.GetMessageHandlerTypes(), assemblyServiceProvider);
        }
    }

    private void RedirectCommonServices(IServiceCollection assemblyServices, ServiceHostContext context)
    {
        assemblyServices.AddSingleton(_ => applicationServiceProvider.GetRequiredService<ILoggerFactory>());
        assemblyServices.AddTransient(_ => applicationServiceProvider.GetRequiredService<ILogger>());
        assemblyServices.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        assemblyServices.AddSingleton(_ => applicationServiceProvider.GetRequiredService<IServiceHostInfo>());
        assemblyServices.AddSingleton(_ => context.Environment);

        assemblyServices.AddSingleton(_ => applicationServiceProvider.GetRequiredService<IConfiguration>());

        assemblyServices.AddSingleton(_ => applicationServiceProvider.GetRequiredService<IMessagingInfrastructure>());
        assemblyServices.AddSingleton(_ => applicationServiceProvider.GetRequiredService<IStorageInfrastructure>());
    }

    private void AddApplicationMessageHandlersToDispatcher()
        => AddMessageHandlersToDispatcher(hostMessageHandlerTypes.GetMessageHandlerTypes(), applicationServiceProvider);

    private void AddMessageHandlersToDispatcher(IEnumerable<Type> messageHandlerTypes, IServiceProvider serviceProvider)
    {
        foreach (var type in messageHandlerTypes)
        {
            logger.LogDebug("Add message handler factory function to dispatcher: {MessageHandlerType}.", type.FullName);
            messageDispatcher.AddHandler(type, () => (IMessageHandler)serviceProvider.GetRequiredService(type));
        }
    }

    private string GetEnvironment()
    {
        var environment = configuration["environment"];

        if (string.IsNullOrWhiteSpace(environment))
        {
            environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrEmpty(environment) ? "Production" : environment;
    }

    private ServiceHostEnvironment BuildServiceHostEnvironment()
        => new()
        {
            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name,
            EnvironmentName = GetEnvironment()
        };

    private ServiceHostContext BuildServiceHostContext()
        => new()
        {
            Configuration = applicationServiceProvider.GetRequiredService<IConfiguration>(),
            Environment = BuildServiceHostEnvironment(),
            HostInfo = applicationServiceProvider.GetRequiredService<IServiceHostInfo>()
        };

#pragma warning disable CS0618 // Type or member is obsolete
    private sealed record HostedServiceContainer(IHostedServiceAsync? AsyncService, IHostedService? Service);
#pragma warning restore CS0618 // Type or member is obsolete
}
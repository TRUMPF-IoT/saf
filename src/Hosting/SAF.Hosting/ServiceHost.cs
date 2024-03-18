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
using Abstractions;

/// <summary>
/// Central entry point for initializing and starting the services used.
/// </summary>
public class ServiceHost(
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
    private readonly List<IHostedServiceBase> _services = [];

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
            logger.LogInformation("Stopping all services took {serviceShutdownTime:N0} ns",
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

        logger.LogInformation("Starting all services took {serviceStartUpTime:N0} ns",
            stopWatch.Elapsed.TotalMilliseconds * 1000000);
    }

    private async Task StartServicesAsync(IEnumerable<IHostedServiceBase> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !linkedCts.Token.IsCancellationRequested))
            {
                logger.LogDebug("Starting service: {serviceName}", service.GetType().Name);

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                switch (service)
                {
                    case IHostedServiceAsync asyncService:
                        await asyncService.StartAsync(linkedCts.Token);
                        break;
                    case IHostedService syncService:
                        syncService.Start();
                        break;
                }

                serviceStopWatch.Stop();

                logger.LogInformation(
                    "Started service: {serviceName}, start-up took {serviceStartUpTime:N0} ns",
                        service.GetType().Name, serviceStopWatch.Elapsed.TotalMilliseconds * 1000000);
            }
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

    private async Task StopServicesAsync(IEnumerable<IHostedServiceBase> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !cancelToken.IsCancellationRequested))
            {
                logger.LogDebug("Stopping service: {serviceName}", service.GetType().Name);

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                switch (service)
                {
                    case IHostedServiceAsync asyncService:
                        await asyncService.StopAsync(linkedCts.Token);
                        break;
                    case IHostedService syncService:
                        syncService.Stop();
                        break;
                }

                serviceStopWatch.Stop();

                logger.LogInformation("Stopped service: {serviceName}, shutdown took {serviceShutdownTime:N0} ns",
                    service.GetType().Name, serviceStopWatch.Elapsed.TotalMilliseconds * 1000000);
            }
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

    private void InitializeServices()
    {
        var context = BuildServiceHostContext();

        foreach(var manifest in serviceAssemblyManager.GetServiceAssemblyManifests())
        {
            logger.LogInformation("Initializing service assembly: {serviceName}.", manifest.FriendlyName);

            var assemblyServiceCollection = new ServiceCollection();

            RedirectCommonServices(assemblyServiceCollection, context);
            sharedServiceRegistry.RedirectServices(applicationServiceProvider, assemblyServiceCollection);

            manifest.RegisterDependencies(assemblyServiceCollection, context);

            var assemblyServiceProvider = assemblyServiceCollection.BuildServiceProvider();
            _serviceAssemblyServiceProviders.Add(assemblyServiceProvider);

            var servicesToAdd = assemblyServiceProvider.GetServices<IHostedServiceBase>();
            _services.AddRange(servicesToAdd);

            var assemblyMessageHandlerTypes = new ServiceMessageHandlerTypes(assemblyServiceCollection);
            AddMessageHandlersToDispatcher(assemblyMessageHandlerTypes.GetMessageHandlerTypes(), assemblyServiceProvider);
        }
    }

    private void RedirectCommonServices(IServiceCollection assemblyServices, IServiceHostContext context)
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
            logger.LogDebug("Add message handler factory function to dispatcher: {messageHandlerType}.", type.FullName);
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

    private IServiceHostContext BuildServiceHostContext()
        => new ServiceHostContext
        {
            Configuration = applicationServiceProvider.GetRequiredService<IConfiguration>(),
            Environment = BuildServiceHostEnvironment(),
            HostInfo = applicationServiceProvider.GetRequiredService<IServiceHostInfo>()
        };
}
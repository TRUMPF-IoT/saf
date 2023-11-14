// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;

namespace SAF.Hosting;

/// <summary>
/// Central entry point for initializing and starting the services used.
/// </summary>
public sealed class ServiceHost(
    ILogger<ServiceHost> logger,
    IServiceProvider applicationServiceProvider,
    IServiceAssemblyManager serviceAssemblyManager,
    IServiceMessageDispatcher messageDispatcher,
    IConfiguration configuration)
    : Microsoft.Extensions.Hosting.IHostedService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _applicationServiceProvider = applicationServiceProvider;
    private readonly IServiceAssemblyManager _serviceAssemblyManager = serviceAssemblyManager;
    private readonly IServiceMessageDispatcher _messageDispatcher = messageDispatcher;
    private readonly IConfiguration _configuration = configuration;

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
            _logger.LogInformation($"Stopping all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");

            _services.Clear();
            _serviceAssemblyServiceProviders.ForEach(service => service.Dispose());
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

    public void Dispose()
    {
        StopAsync(CancellationToken.None).Wait();
    }

    private async Task StartInternalAsync(CancellationToken token)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        InitializeServices();
        AddRuntimeMessageHandlersToDispatcher();

        await StartServicesAsync(_services, token);

        stopWatch.Stop();

        _logger.LogInformation("Starting all services took {serviceStartUpTime:N0} ms", stopWatch.Elapsed.TotalMilliseconds);
    }

    private async Task StartServicesAsync(IEnumerable<IHostedServiceBase> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !linkedCts.Token.IsCancellationRequested))
            {
                _logger.LogDebug($"Starting service: {service.GetType().Name}");

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

                _logger.LogInformation(
                    $"Started service: {service.GetType().Name}, start-up took {serviceStopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
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
                _logger.LogDebug($"Stopping service: {service.GetType().Name}");

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

                _logger.LogInformation($"Stopped service: {service.GetType().Name}, shutdown took {serviceStopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
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

        foreach(var manifest in _serviceAssemblyManager.GetServiceAssemblyManifests())
        {
            _logger.LogInformation("Initializing service assembly: {serviceName}.", manifest.FriendlyName);

            var assemblyServiceCollection = new ServiceCollection();

            RedirectCommonServicesFromOuterContainer(assemblyServiceCollection);

            //TODO: redirect toolbox services

            manifest.RegisterDependencies(assemblyServiceCollection, context);

            var assemblyServiceProvider = assemblyServiceCollection.BuildServiceProvider();
            _serviceAssemblyServiceProviders.Add(assemblyServiceProvider);

            var servicesToAdd = assemblyServiceProvider.GetServices<IHostedServiceBase>();
            _services.AddRange(servicesToAdd);

            var messageHandlerType = typeof(IMessageHandler);
            foreach (var messageHandlerServiceType in assemblyServiceCollection
                         .Where(s => messageHandlerType.IsAssignableFrom(s.ServiceType))
                         .Select(s => s.ServiceType))
            {
                // keep a reference to the providing service provider within the message dispatcher for every registered message handler
                _logger.LogDebug("Add message handler factory function to dispatcher: {messageHandlerType}.", messageHandlerServiceType.FullName!);
                _messageDispatcher.AddHandler(messageHandlerServiceType.FullName,
                    () => (IMessageHandler) assemblyServiceProvider.GetRequiredService(messageHandlerServiceType));
            }
        }
    }

    private void RedirectCommonServicesFromOuterContainer(IServiceCollection assemblyServices)
    {
        assemblyServices.AddSingleton(_ => _applicationServiceProvider.GetRequiredService<IConfiguration>());
                
        assemblyServices.AddSingleton(_ => _applicationServiceProvider.GetRequiredService<IMessagingInfrastructure>());
        assemblyServices.AddSingleton(_ => _applicationServiceProvider.GetRequiredService<IStorageInfrastructure>());

        assemblyServices.AddTransient(_ => _applicationServiceProvider.GetRequiredService<ILogger>());
        assemblyServices.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        assemblyServices.AddSingleton(_ => _applicationServiceProvider.GetRequiredService<ILoggerFactory>());
        assemblyServices.AddSingleton(_ => _applicationServiceProvider.GetRequiredService<IServiceHostInfo>());
    }

    private void AddRuntimeMessageHandlersToDispatcher()
    {
        foreach(var runtimeApplicationMessageHandler in _applicationServiceProvider.GetServices<IMessageHandler>())
        {
            var type = runtimeApplicationMessageHandler.GetType();
            _logger.LogDebug($"Add runtime message handler factory function to dispatcher: {type.FullName}.");
            _messageDispatcher.AddHandler(type.FullName!, () => runtimeApplicationMessageHandler);
        }
    }

    private string GetEnvironment()
    {
        var environment = _configuration["environment"];

        if(string.IsNullOrWhiteSpace(environment))
            environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.IsNullOrEmpty(environment) ? "Production" : environment;
    }

    private ServiceHostEnvironment BuildServiceHostEnvironment()
    {
        return new()
        {
            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name,
            EnvironmentName = GetEnvironment()
        };
    }

    private IServiceHostContext BuildServiceHostContext()
    {
        return new ServiceHostContext
        {
            Configuration = _applicationServiceProvider.GetRequiredService<IConfiguration>(),
            Environment = BuildServiceHostEnvironment(),
            HostInfo = _applicationServiceProvider.GetRequiredService<IServiceHostInfo>()
        };
    }
}
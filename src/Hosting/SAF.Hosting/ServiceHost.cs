// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;

namespace SAF.Hosting;

/// <summary>
/// Central entry point for initializing and starting the services used.
/// </summary>
public sealed class ServiceHost : Microsoft.Extensions.Hosting.IHostedService, IDisposable
{
    private readonly IServiceProvider _runtimeApplicationServiceProvider;
    private readonly ILogger _log;
    private readonly IEnumerable<IServiceAssemblyManifest> _serviceAssemblies;
    private readonly IServiceMessageDispatcher _messageDispatcher;

    private readonly IConfiguration? _configuration;
    private readonly ServiceHostEnvironment _environment;
    private readonly IServiceHostContext _context;

    private readonly List<IHostedServiceBase> _services = new();

    public ServiceHost(
        IServiceProvider runtimeApplicationServiceProvider,
        ILogger<ServiceHost>? log,
        IServiceMessageDispatcher messageDispatcher,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
    {
        _runtimeApplicationServiceProvider = runtimeApplicationServiceProvider;
        _log = log ?? NullLogger<ServiceHost>.Instance;
        _messageDispatcher = messageDispatcher;
        _serviceAssemblies = serviceAssemblies;

        _configuration = _runtimeApplicationServiceProvider.GetService<IConfiguration>();
        _environment = BuildServiceHostEnvironment();
        _context = BuildServiceHostContext();
    }

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

    private async Task StartInternalAsync(CancellationToken token)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        InitializeServices();
        AddRuntimeMessageHandlersToDispatcher();

        await StartServicesAsync(_services, token);

        stopWatch.Stop();

        _log.LogInformation($"Starting all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
    }

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
            _log.LogInformation($"Stopping all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");

            _services.Clear();
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

    private async Task StartServicesAsync(IEnumerable<IHostedServiceBase> services, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !linkedCts.Token.IsCancellationRequested))
            {
                _log.LogDebug($"Starting service: {service.GetType().Name}");

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

                _log.LogInformation(
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
                _log.LogDebug($"Stopping service: {service.GetType().Name}");

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

                _log.LogInformation($"Stopped service: {service.GetType().Name}, shutdown took {serviceStopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
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
            Configuration = _runtimeApplicationServiceProvider.GetService<IConfiguration>(),
            Environment = _environment,
            HostInfo = _runtimeApplicationServiceProvider.GetService<IHostInfo>()
        };
    }

    private void InitializeServices()
    {
        foreach(var manifest in _serviceAssemblies)
        {
            _log.LogInformation($"Initializing service assembly: {manifest.FriendlyName}.");

            var assemblyServiceCollection = new ServiceCollection();

            RedirectCommonServicesFromOuterContainer(assemblyServiceCollection);

            manifest.RegisterDependencies(assemblyServiceCollection, _context);
            var assemblyServiceProvider = assemblyServiceCollection.BuildServiceProvider();

            var servicesToAdd = assemblyServiceProvider.GetServices<IHostedServiceBase>();
            _services.AddRange(servicesToAdd);

            var messageHandlerType = typeof(IMessageHandler);
            foreach (var messageHandlerServiceType in assemblyServiceCollection
                         .Where(s => messageHandlerType.IsAssignableFrom(s.ServiceType))
                         .Select(s => s.ServiceType))
            {
                // keep a reference to the providing service provider within the dispatcher for every registered message handler
                _log.LogDebug(
                    $"Add message handler factory function to dispatcher: {messageHandlerServiceType.FullName}.");
                _messageDispatcher.AddHandler(messageHandlerServiceType.FullName!,
                    () => (IMessageHandler) assemblyServiceProvider.GetRequiredService(messageHandlerServiceType));
            }
        }
    }

    private void RedirectCommonServicesFromOuterContainer(IServiceCollection assemblyServices)
    {
        assemblyServices.AddSingleton(_ => _runtimeApplicationServiceProvider.GetRequiredService<IConfiguration>());
                
        assemblyServices.AddSingleton(_ => _runtimeApplicationServiceProvider.GetRequiredService<IMessagingInfrastructure>());
        assemblyServices.AddSingleton(_ => _runtimeApplicationServiceProvider.GetRequiredService<IStorageInfrastructure>());

        assemblyServices.AddTransient(_ => _runtimeApplicationServiceProvider.GetRequiredService<ILogger>());
        assemblyServices.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        assemblyServices.AddSingleton(_ => _runtimeApplicationServiceProvider.GetRequiredService<ILoggerFactory>());
        assemblyServices.AddSingleton(_ => _runtimeApplicationServiceProvider.GetRequiredService<IHostInfo>());
    }

    private void AddRuntimeMessageHandlersToDispatcher()
    {
        foreach(var runtimeApplicationMessageHandler in _runtimeApplicationServiceProvider.GetServices<IMessageHandler>())
        {
            var type = runtimeApplicationMessageHandler.GetType();
            _log.LogDebug($"Add runtime message handler factory function to dispatcher: {type.FullName}.");
            _messageDispatcher.AddHandler(type.FullName!, () => runtimeApplicationMessageHandler);
        }
    }

    private string GetEnvironment()
    {
        string? environment = null;

        if (_configuration != null)
            environment = _configuration["environment"];

        if(string.IsNullOrWhiteSpace(environment))
            environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.IsNullOrEmpty(environment) ? "Production" : environment;
    }
}
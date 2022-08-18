// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

    private readonly IConfiguration _configuration;
    private readonly ServiceHostEnvironment _environment;
    private readonly IServiceHostContext _context;

    private readonly List<IHostedService> _services = new();
    private readonly List<IHostedServiceAsync> _asyncServices = new();

    public ServiceHost(
        IServiceProvider runtimeApplicationServiceProvider,
        ILogger<ServiceHost> log,
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

        InitializeServices();
        AddRuntimeMessageHandlersToDispatcher();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await StartServicesAsync(_services, (service, token) => Task.Run(service.Start, token), linkedCts.Token);
            await StartServicesAsync(_asyncServices, (service, token) => service.StartAsync(token), linkedCts.Token);

            stopWatch.Stop();
            _log.LogInformation($"Starting all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
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

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_services.Count == 0 && _asyncServices.Count == 0)
            return;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await StopServicesAsync(_asyncServices, (service, token) => service.StopAsync(token), linkedCts.Token);
            await StopServicesAsync(_services, (service, token) => Task.Run(service.Stop, token), linkedCts.Token);

            stopWatch.Stop();
            _log.LogInformation($"Stopping all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");

            _asyncServices.Clear();
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

    private async Task StartServicesAsync<TService>(IEnumerable<TService> services, Func<TService, CancellationToken, Task> startAction, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !linkedCts.Token.IsCancellationRequested))
            {
                _log.LogDebug($"Starting service: {service.GetType().Name}");

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                await startAction(service, linkedCts.Token);

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

    private async Task StopServicesAsync<TService>(IEnumerable<TService> services, Func<TService, CancellationToken, Task> stopAction, CancellationToken cancelToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        try
        {
            foreach (var service in services.TakeWhile(_ => !cancelToken.IsCancellationRequested))
            {
                _log.LogDebug($"Stopping service: {service.GetType().Name}");

                var serviceStopWatch = new Stopwatch();
                serviceStopWatch.Start();

                await stopAction(service, cancelToken);

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

            var servicesToAdd = assemblyServiceProvider.GetServices<IHostedService>();
            _services.AddRange(servicesToAdd);
            var asyncServicesToAdd = assemblyServiceProvider.GetServices<IHostedServiceAsync>();
            _asyncServices.AddRange(asyncServicesToAdd);

            var messageHandlerType = typeof(IMessageHandler);
            foreach(var messageHandlerRegistration in assemblyServiceCollection.Where(s => messageHandlerType.IsAssignableFrom(s.ServiceType)))
            {
                // keep a reference to the providing service provider within the dispatcher for every registered message handler
                _log.LogDebug($"Add message handler factory function to dispatcher: {messageHandlerRegistration.ServiceType.FullName}.");
                _messageDispatcher.AddHandler(messageHandlerRegistration.ServiceType.FullName,
                    () => (IMessageHandler) assemblyServiceProvider.GetRequiredService(messageHandlerRegistration.ServiceType));
            }
        }
    }

    private void RedirectCommonServicesFromOuterContainer(IServiceCollection assemblyServices)
    {
        assemblyServices.AddSingleton(sp => _runtimeApplicationServiceProvider.GetService<IConfiguration>());
                
        assemblyServices.AddSingleton(sp => _runtimeApplicationServiceProvider.GetService<IMessagingInfrastructure>());
        assemblyServices.AddSingleton(sp => _runtimeApplicationServiceProvider.GetService<IStorageInfrastructure>());

        assemblyServices.AddTransient(sp => _runtimeApplicationServiceProvider.GetService<ILogger>());
        assemblyServices.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        assemblyServices.AddSingleton(sp => _runtimeApplicationServiceProvider.GetService<ILoggerFactory>());
        assemblyServices.AddSingleton(sp => _runtimeApplicationServiceProvider.GetService<IHostInfo>());
    }

    private void AddRuntimeMessageHandlersToDispatcher()
    {
        foreach(var runtimeApplicationMessageHandler in _runtimeApplicationServiceProvider.GetServices<IMessageHandler>())
        {
            var type = runtimeApplicationMessageHandler.GetType();
            _log.LogDebug($"Add runtime message handler factory function to dispatcher: {type.FullName}.");
            _messageDispatcher.AddHandler(type.FullName, () => runtimeApplicationMessageHandler);
        }
    }

    private string GetEnvironment()
    {
        string environment = null;

        if (_configuration != null)
            environment = _configuration["environment"];

        if(string.IsNullOrWhiteSpace(environment))
            environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

        return string.IsNullOrEmpty(environment) ? "Production" : environment;
    }
}
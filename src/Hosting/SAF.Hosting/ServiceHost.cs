// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
public class ServiceHost : IDisposable
{
    private readonly IServiceProvider _runtimeApplicationServiceProvider;
    private readonly ILogger _log;
    private readonly IEnumerable<IServiceAssemblyManifest> _serviceAssemblies;
    private readonly IServiceMessageDispatcher _messageDispatcher;

    private readonly IConfiguration _configuration;
    private readonly ServiceHostEnvironment _environment;
    private readonly IServiceHostContext _context;

    private readonly List<IHostedService> _services = new();

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

    public void StartServices()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        foreach (var service in _services)
        {
            _log.LogDebug($"Starting service: {service.GetType().Name}");

            var serviceStopWatch = new Stopwatch();
            serviceStopWatch.Start();

            service.Start();

            serviceStopWatch.Stop();

            _log.LogInformation($"Started service: {service.GetType().Name}, start-up took {serviceStopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
        }

        stopWatch.Stop();
        _log.LogInformation($"Starting all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
    }

    public void Dispose()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        foreach (var service in _services)
        {
            _log.LogDebug($"Stopping service: {service.GetType().Name}");

            var serviceStopWatch = new Stopwatch();
            serviceStopWatch.Start();

            service.Stop();

            serviceStopWatch.Stop();

            _log.LogInformation($"Stopped service: {service.GetType().Name}, shutdown took {serviceStopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
        }

        stopWatch.Stop();
        _log.LogInformation($"Stopping all services took {stopWatch.Elapsed.TotalMilliseconds * 1000000:N0} ns");
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
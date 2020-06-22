// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;

namespace SAF.Hosting
{
    public class ServiceHost : IDisposable
    {
        private readonly IServiceProvider _runtimeApplicationServiceProvider;
        private readonly ILogger _log;
        private readonly IEnumerable<IServiceAssemblyManifest> _serviceAssemblies;
        private readonly IServiceMessageDispatcher _messageDispatcher;

        private readonly List<IHostedService> _services = new List<IHostedService>();

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

            InitializeServices();
            AddRuntimeMessageHandlersToDispatcher();
        }

        public void StartServices()
        {
            foreach(var service in _services)
            {
                service.Start();
                _log.LogInformation($"Started service: {service.GetType().Name}");
            }
        }

        public void Dispose()
        {
            foreach(var service in _services)
            {
                service.Stop();
                _log.LogInformation($"Stopped service: {service.GetType().Name}");
            }
        }

        private void InitializeServices()
        {
            foreach(var manifest in _serviceAssemblies)
            {
                _log.LogInformation($"Initializing service assembly: {manifest.FriendlyName}.");

                var assemblyServiceCollection = new ServiceCollection();

                RedirectCommonServicesFromOuterContainer(assemblyServiceCollection);

                manifest.RegisterDependencies(assemblyServiceCollection);
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
    }
}
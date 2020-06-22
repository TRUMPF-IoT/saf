// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using SAF.Common;

[assembly: InternalsVisibleTo("SAF.Messaging.Routing.Tests")]
namespace SAF.Messaging.Routing
{
    /// <summary>
    ///     Some extension methods to simplify service registration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a IMessagingInfrastructure to the container used to provide message routing.
        /// </summary>
        /// <param name="serviceCollection">The service collection to add the IMessagingInfrastructure.</param>
        /// <param name="configure">Action used to update configuration for message routes.</param>
        /// <returns>The serviceCollection for chaining.</returns>
        public static IServiceCollection AddRoutingMessagingInfrastructure(this IServiceCollection serviceCollection, Action<Configuration> configure)
        {
            var config = new Configuration();
            configure(config);

            return serviceCollection.AddRoutingMessagingInfrastructure(config)
                .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IRoutingMessagingInfrastructure>());
        }

        private static IServiceCollection AddRoutingMessagingInfrastructure(this IServiceCollection serviceCollection, Configuration config)
        {
            var basePath = string.IsNullOrEmpty(config.BasePath) ? AppDomain.CurrentDomain.BaseDirectory : config.BasePath;
            var searchFilenamePattern = string.IsNullOrEmpty(config.SearchFilenamePattern) ? ".*" : config.SearchFilenamePattern;
            var searchPath = string.IsNullOrEmpty(config.SearchPath) ? "SAF.Messaging.*.dll" : config.SearchPath;

            var results = SearchMessagingAssemblies(basePath, searchPath, searchFilenamePattern);
            foreach(var assembly in results)
            {
                var loadedAssembly = Assembly.LoadFrom(assembly);
                var manifestType = loadedAssembly.GetExportedTypes().SingleOrDefault(t => t.IsClass && typeof(IMessagingAssemblyManifest).IsAssignableFrom(t));
                if(manifestType == default)
                {
                    continue;
                }

                var messagingType = loadedAssembly.GetExportedTypes().SingleOrDefault(t => typeof(IMessagingInfrastructure).IsAssignableFrom(t));
                if(messagingType == null) continue;

                var messagingConfigs = config.Routings.Where(r => r.Messaging.Type == messagingType.Name).Select(t => t.Messaging);
                var manifest = Activator.CreateInstance(manifestType) as IMessagingAssemblyManifest;
                if (manifest == null) continue;
                foreach(var messageConfig in messagingConfigs)
                {
                    manifest.RegisterDependencies(serviceCollection, messageConfig);
                }
            }

            return serviceCollection.AddTransient<IRoutingMessagingInfrastructure>(sp =>
                new Messaging(sp.GetService<ILogger<Messaging>>(), BuildMessageRouting(serviceCollection, sp, config)));
        }

        private static MessageRouting[] BuildMessageRouting(IServiceCollection serviceCollection, IServiceProvider serviceProvider, Configuration config)
        {
            return config.Routings
                .Select(r =>
                {
                    var serviceType = serviceCollection.FirstOrDefault(sd => sd.ServiceType.Name == r.Messaging.Type)?.ServiceType;
                    if(serviceType == null)
                        throw new FileNotFoundException($"Messaging DLL not installed for messaging type {r.Messaging.Type}");

                    var routing = new MessageRouting(BuildMessagingInfrastructure(serviceProvider, serviceType, r.Messaging))
                    {
                        PublishPatterns = r.PublishPatterns,
                        SubscriptionPatterns = r.SubscriptionPatterns
                    };
                    return routing;
                }).ToArray();
        }

        private static IMessagingInfrastructure BuildMessagingInfrastructure(IServiceProvider serviceProvider, Type serviceType, MessagingConfiguration config)
        {
            var factoryType = typeof(Func<,>).MakeGenericType(typeof(MessagingConfiguration), serviceType);
            if (serviceProvider.GetService(factoryType) is Delegate factoryFunc)
            {
                return factoryFunc.DynamicInvoke(config) as IMessagingInfrastructure;
            }
            return serviceProvider.GetService(serviceType) as IMessagingInfrastructure;
        }

        internal static IEnumerable<string> SearchMessagingAssemblies(string basePath, string searchPath, string fileNameFilterRegEx)
        {
            if (basePath == null) throw new ArgumentNullException(nameof(basePath));
            if (searchPath == null) throw new ArgumentNullException(nameof(searchPath));
            if (fileNameFilterRegEx == null) throw new ArgumentNullException(nameof(fileNameFilterRegEx));

            var serviceMatcher = new Matcher();
            foreach (var pattern in searchPath.Split(';'))
            {
                if (pattern.StartsWith("|")) serviceMatcher.AddExclude(pattern.Substring(1));
                else serviceMatcher.AddInclude(pattern);
            }
            IList<string> results = serviceMatcher.GetResultsInFullPath(basePath).ToList();

            var serviceAssemblyNameRegEx = new Regex(fileNameFilterRegEx, RegexOptions.IgnoreCase);
            results = results.Where(r => serviceAssemblyNameRegEx.IsMatch(Path.GetFileName(r))).ToList();

            return results;
        }
    }
}
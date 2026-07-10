// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

[assembly: InternalsVisibleTo("SAF.Messaging.Routing.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Messaging.Routing;

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
        var basePath = string.IsNullOrEmpty(config.BasePath) ? AppContext.BaseDirectory : config.BasePath;
        var searchFilenamePattern = string.IsNullOrEmpty(config.SearchFilenamePattern) ? ".*" : config.SearchFilenamePattern;
        var searchPath = string.IsNullOrEmpty(config.SearchPath) ? "SAF.Messaging.*.dll" : config.SearchPath;

        var results = SearchMessagingAssemblies(basePath, searchPath, searchFilenamePattern);
        foreach(var assembly in results)
        {
            var loadedAssembly = Assembly.LoadFrom(assembly);
            var manifestType = loadedAssembly.GetExportedTypes().SingleOrDefault(t => t.IsClass && typeof(IPluginManifest).IsAssignableFrom(t));
            if(manifestType == default)
            {
                continue;
            }

            var messagingType = loadedAssembly.GetExportedTypes().SingleOrDefault(t => typeof(IMessagingInfrastructure).IsAssignableFrom(t));
            if(messagingType == null) continue;

            var messagingConfigs = config.Routings.Where(r => r.Messaging.Type == messagingType.Name).Select(t => t.Messaging);
            foreach(var messageConfig in messagingConfigs)
            {
                ConfigureMessagingInfrastructure(loadedAssembly, serviceCollection, messagingType, messageConfig);
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
            return (IMessagingInfrastructure)factoryFunc.DynamicInvoke(config)!;
        }
        return (IMessagingInfrastructure)serviceProvider.GetService(serviceType)!;
    }

    internal static IEnumerable<string> SearchMessagingAssemblies(string basePath, string searchPath, string fileNameFilterRegEx)
    {
        if (basePath == null) throw new ArgumentNullException(nameof(basePath));
        if (searchPath == null) throw new ArgumentNullException(nameof(searchPath));
        if (fileNameFilterRegEx == null) throw new ArgumentNullException(nameof(fileNameFilterRegEx));

        var serviceMatcher = new Matcher();
        foreach (var pattern in searchPath.Split(';'))
        {
            if (pattern.StartsWith('|')) serviceMatcher.AddExclude(pattern[1..]);
            else serviceMatcher.AddInclude(pattern);
        }
        IList<string> results = serviceMatcher.GetResultsInFullPath(basePath).ToList();

        var serviceAssemblyNameRegEx = new Regex(fileNameFilterRegEx, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(30));
        results = results.Where(r => serviceAssemblyNameRegEx.IsMatch(Path.GetFileName(r))).ToList();

        return results;
    }

    private static void ConfigureMessagingInfrastructure(Assembly loadedAssembly, IServiceCollection serviceCollection, Type messagingType, MessagingConfiguration messagingConfiguration)
    {
        var extensionType = loadedAssembly.GetTypes().SingleOrDefault(t => t.IsAbstract && t.IsSealed && t.Name == "ServiceCollectionExtensions");
        if (extensionType == null)
        {
            throw new InvalidOperationException($"No service collection extensions found in {loadedAssembly.FullName}.");
        }

        var methodName = GetMessagingInfrastructureRegistrationMethodName(messagingType);
        var registrationMethod = extensionType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(IServiceCollection), typeof(MessagingConfiguration)],
            modifiers: null);

        if (registrationMethod == null)
        {
            throw new InvalidOperationException($"Can't find messaging registration method {methodName} in {loadedAssembly.FullName}.");
        }

        _ = registrationMethod.Invoke(null, [serviceCollection, messagingConfiguration]);
    }

    private static string GetMessagingInfrastructureRegistrationMethodName(Type messagingType)
    {
        var messagingTypeName = messagingType.Name;

        if (messagingTypeName.StartsWith('I') && messagingTypeName.EndsWith("MessagingInfrastructure", StringComparison.Ordinal))
        {
            return $"Add{messagingTypeName[1..^"MessagingInfrastructure".Length]}MessagingInfrastructure";
        }

        if (messagingTypeName.StartsWith('I') && messagingTypeName.EndsWith("Infrastructure", StringComparison.Ordinal))
        {
            return $"Add{messagingTypeName[1..^"Infrastructure".Length]}Infrastructure";
        }

        return $"Add{messagingTypeName[1..]}";
    }
}



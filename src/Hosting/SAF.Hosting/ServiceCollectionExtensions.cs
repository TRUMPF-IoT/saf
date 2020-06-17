// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Hosting.Diagnostics;

[assembly: InternalsVisibleTo("SAF.Hosting.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHost(this IServiceCollection services, Action<Configuration> configure,
            ILogger logger = null)
            => services.AddHost(configure, hi =>
            {
                hi.ServiceHostType = "Unknown";
                hi.FileSystemUserBasePath = "tempfs";
                hi.FileSystemInstallationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }, logger);

        public static IServiceCollection AddHost(this IServiceCollection services, Action<Configuration> configure, Action<HostInfo> configureHostInfo = null, ILogger logger = null)
        {
            logger = logger ?? NullLogger.Instance;

            var config = new Configuration();
            configure(config);
            config.BasePath = string.IsNullOrEmpty(config.BasePath) ? AppDomain.CurrentDomain.BaseDirectory : config.BasePath;
            config.SearchFilenamePattern = string.IsNullOrEmpty(config.SearchFilenamePattern) ? ".*" : config.SearchFilenamePattern;
            if (string.IsNullOrWhiteSpace(config.SearchPath))
            {
                const string errorLog = "Configuration setting \"SearchPath\" not set!";
                logger.LogCritical(errorLog);
                throw new InvalidOperationException(errorLog);
            }
            
            logger.LogInformation($"Searching SAF service assemblies using BasePath: {config.BasePath}, SearchPath: {config.SearchPath}, SearchFilenamePattern: {config.SearchFilenamePattern}");
            var results = SearchServiceAssemblies(config.BasePath, config.SearchPath, config.SearchFilenamePattern).ToList();
            logger.LogInformation($"Found {results.Count} possible SAF service assemblies [{string.Join(", ", results.Select(_ => Path.GetFileName(_)))}]");

            var loaded = 0;
            foreach(var assembly in results)
            {
                var loadedAssembly = Assembly.LoadFrom(assembly);
                logger.LogInformation("Loading assembly: {0}, Version: {1}", assembly, loadedAssembly.GetName().Version.ToString());

                var manifest = loadedAssembly.GetExportedTypes().SingleOrDefault(t => t.IsClass && typeof(IServiceAssemblyManifest).IsAssignableFrom(t));

                if(manifest == default(Type))
                {
                    logger.LogError($"Assembly {loadedAssembly.FullName} skipped: a valid assembly should contain exactly one public manifest.");
                    continue;
                }

                loaded++;
                services.AddSingleton(typeof(IServiceAssemblyManifest), manifest);
            }

            logger.LogInformation($"Loaded {loaded} assemblies.");

            services.AddSingleton<ServiceHost>();
            services.AddSingleton<IServiceMessageDispatcher, MessageDispatcher>();

            services.AddSingleton<IHostInfo>(sp =>
            {
                var hi = new HostInfo();
                configureHostInfo?.Invoke(hi);
                hi.Id = string.IsNullOrWhiteSpace(hi.Id) ? GetOrInitializeStoredHostId(sp) : hi.Id;
                return hi;
            });

            return services;
        }

        public static IServiceCollection AddHostDiagnostics(this IServiceCollection services)
            => services.AddSingleton<ServiceHostDiagnostics>();

        public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration config)
            => services.AddSingleton(config);

        /// <summary>
        /// Search SAF service assemblies
        /// </summary>
        /// <param name="basePath">Base path where search starts</param>
        /// <param name="searchPath">Search path (glob pattern). The ';' character is used as delimiter for multiple patterns and '|' as prefix for exclusions</param>
        /// <param name="fileNameFilterRegEx">Filter regex that each filename must satisfy (if in doubt use ".*")</param>
        /// <returns></returns>
        internal static IEnumerable<string> SearchServiceAssemblies(string basePath, string searchPath, string fileNameFilterRegEx)
        {
            // This function probably should be moved somewhere else
            if (basePath == null) throw new ArgumentNullException(nameof(basePath));
            if (searchPath == null) throw new ArgumentNullException(nameof(searchPath));
            if (fileNameFilterRegEx == null) throw new ArgumentNullException(nameof(fileNameFilterRegEx));

            // Use matcher to find service  assemblies
            var serviceMatcher = new Matcher();
            foreach (var pattern in searchPath.Split(';'))
            {
                if (pattern.StartsWith("|")) serviceMatcher.AddExclude(pattern.Substring(1));
                else serviceMatcher.AddInclude(pattern);
            }
            IList<string> results = serviceMatcher.GetResultsInFullPath(basePath).ToList();

            // Filter assemblies using RegEx
            var serviceAssemblyNameRegEx = new Regex(fileNameFilterRegEx, RegexOptions.IgnoreCase);
            results = results.Where(_ => serviceAssemblyNameRegEx.IsMatch(Path.GetFileName(_))).ToList();

            return results;
        }

        /// <summary>
        /// Default behavior for determining the SAF host id. This method is used, if the host id is not set in the configuration callback.
        /// - Read host id from storage key "saf/hostid"
        /// - If key is not set, generate Guid for host id and try to set it in storage
        /// </summary>
        /// <param name="sp"></param>
        /// <returns>The host id from storage key "saf/hostid" or an Guid</returns>
        private static string GetOrInitializeStoredHostId(IServiceProvider sp)
        {
            const string storageKey = "saf/hostid";
            var storage = sp.GetService<IStorageInfrastructure>();

            var id = storage?.GetString(storageKey);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
                storage?.Set(storageKey, id);
            }
            return id;
        }

    }
}
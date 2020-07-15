// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nsCDEngine.BaseClasses;
using SAF.Common;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Configuration;
using SAF.Messaging.Cde;
using SAF.Messaging.Cde.Diagnostics;

[assembly: InternalsVisibleTo("SAF.Hosting.Cde.Tests")]

namespace SAF.Hosting.Cde
{
    public class ServiceHost : IDisposable
    {
        private readonly ServiceProvider _baseServiceProvider;
        private readonly ServiceProvider _applicationServiceProvider;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ServiceHost(string searchPattern = null, string userBasePath = null)
            : this(null, searchPattern, userBasePath)
        { }

        public ServiceHost(Action<IServiceCollection> installInfrastructureServices, string searchPattern = null, string userBasePath = null)
        {
            searchPattern = searchPattern ?? "SAF.*.dll";
            userBasePath = userBasePath ?? Path.Combine(TheBaseAssets.MyServiceHostInfo.BaseDirectory, "ClientBin", "saf");

            var applicationServices = new ServiceCollection();
            applicationServices.AddLogging(l => 
                l.AddProvider(new LoggerProvider()) // CDE logging
                 .SetMinimumLevel(Logger.Convert(TheBaseAssets.MyServiceHostInfo.DebugLevel)) // apply app.config configuration
            );

            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("safsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _baseServiceProvider = applicationServices.BuildServiceProvider();
            var startupLogger = _baseServiceProvider.GetService<ILogger<ServiceHost>>();
            startupLogger.LogInformation($"Starting SAF Host: pattern='{searchPattern}', userBasePath={userBasePath}");

            applicationServices.AddConfiguration(config);
            installInfrastructureServices?.Invoke(applicationServices);

            InstallCdeMessagingServices(installInfrastructureServices, applicationServices);

            if (installInfrastructureServices == null || applicationServices.All(sd => sd.ServiceType != typeof(IStorageInfrastructure)))
                applicationServices.AddCdeStorageInfrastructure();

            applicationServices.AddHost(c =>
            {
                c.SearchPath = searchPattern;
                c.BasePath = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            }, 
            hi =>
            {
                hi.ServiceHostType = $"CDE, AppName={TheBaseAssets.MyServiceHostInfo.ApplicationName}";
                hi.FileSystemUserBasePath = userBasePath;
                hi.FileSystemInstallationPath = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            },
            startupLogger);

            applicationServices.AddHostDiagnostics()
                .AddCdeDiagnostics();

            _applicationServiceProvider = applicationServices.BuildServiceProvider();
            _applicationServiceProvider.UseServiceHost();
            _applicationServiceProvider.UseServiceHostDiagnostics();
            _applicationServiceProvider.UseCdeServiceHostDiagnostics();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _applicationServiceProvider.Dispose();
            _baseServiceProvider.Dispose();
        }

        private void InstallCdeMessagingServices(Action<IServiceCollection> installInfrastructureServices, ServiceCollection applicationServices)
        {
            if (installInfrastructureServices == null || applicationServices.All(sd => sd.ServiceType != typeof(IMessagingInfrastructure)))
            {
                applicationServices.AddSingleton(sp => sp.GetService<ICdeMessagingInfrastructure>() as IMessagingInfrastructure);
            }

            if(applicationServices.All(sd => sd.ServiceType != typeof(ICdeMessagingInfrastructure)))
            {
                applicationServices.AddCdeMessagingInfrastructure();
            }
        }
    }
}
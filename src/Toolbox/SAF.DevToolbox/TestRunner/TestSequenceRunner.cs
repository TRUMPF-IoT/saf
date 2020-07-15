// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.InProcess;
using SAF.Messaging.Redis;

namespace SAF.DevToolbox.TestRunner
{
    public class TestSequenceRunner : IDisposable
    {
        private readonly ServiceCollection _applicationServices;
        private ServiceProvider _applicationServiceProvider;

        private readonly List<Type> _testSequences = new List<Type>();
        private readonly List<TestSequenceTracer> _tracer = new List<TestSequenceTracer>();
        private readonly ILogger<TestSequenceRunner> _mainLogger;
        private readonly IConfigurationRoot _config;
        private readonly List<Action<IMessagingInfrastructure>> _subscribeChannelActions = new List<Action<IMessagingInfrastructure>>();

        private string _traceTestSequencesToPath;
        private TestSequenceTracer _currentTestSequenceTracer;

        private Action<IServiceProvider> _infrastructureInitialization;

        public TestSequenceRunner(string name)
        {
            var environment = GetEnvironment();

            var title = string.IsNullOrWhiteSpace(name) ? "SAF" : $"SAF {name}";
            Console.Title = $"{title} Test Host" + (environment == "production" ? "" : $" ({environment})");

            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings_{environment}.json", optional: true, reloadOnChange: false)
                .Build();

            _applicationServices = new ServiceCollection();
            _applicationServices.AddLogging(l => l.AddConfiguration(_config.GetSection("Logging")).AddConsole());

            var baseServiceProvider = _applicationServices.BuildServiceProvider();
            _mainLogger = baseServiceProvider.GetService<ILogger<TestSequenceRunner>>();
            _mainLogger.LogInformation("Starting test runner console app...");

            _applicationServices.AddConfiguration(_config);
            _applicationServices.AddHost(_config.GetSection("ServiceHost").Bind, hi =>
            {
                hi.ServiceHostType = "Test Sequence Runner";
                hi.FileSystemUserBasePath = "tempfs";
                hi.FileSystemInstallationPath = Directory.GetCurrentDirectory();
            }, _mainLogger);
        }

        public TestSequenceRunner() : this(string.Empty)
        {
            
        }

        public TestSequenceRunner UseCdeInfrastructure()
        {
            _applicationServices.AddCdeInfrastructure(config =>
            {
                _config.GetSection("Cde").Bind(config);
            },
            m => _currentTestSequenceTracer?.MessagingTrace(m));
            _infrastructureInitialization = sp => sp.UseCde();
            return this;
        }

        public TestSequenceRunner UseRedisInfrastructure()
        {
            _applicationServices.AddRedisInfrastructure(config =>
            {
                _config.GetSection("Redis").Bind(config);
            },
            m => _currentTestSequenceTracer?.MessagingTrace(m));
            return this;
        }

        public TestSequenceRunner UseInProcessInfrastructure()
        {
            _applicationServices.AddInProcessMessagingInfrastructure(m => _currentTestSequenceTracer?.MessagingTrace(m))
                .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IInProcessMessagingInfrastructure>());
            return this;
        }

        public TestSequenceRunner TraceTestSequences(string basePath = ".")
        {
            _traceTestSequencesToPath = basePath;
            return this;
        }

        public TestSequenceRunner AddTestSequence<T>() where T : TestSequenceBase
        {
            _applicationServices.AddSingleton<T>();
            _applicationServices.AddTransient<IMessageHandler>(r => r.GetService<T>());

            _testSequences.Add(typeof(T));

            return this;
        }

        public TestSequenceRunner AddTestMessageHandler<T>(params string[] channels) where T : class, IMessageHandler
        {
            _applicationServices.AddSingleton<T>();
            _applicationServices.AddTransient<IMessageHandler>(r => r.GetService<T>());

            foreach (var channel in channels)
            {
                _subscribeChannelActions.Add(mi => mi.Subscribe<T>(channel));
            }

            return this;
        }

        public TestSequenceRunner RegisterTestDependencies(IServiceAssemblyManifest testManifest)
        {
            testManifest?.RegisterDependencies(_applicationServices);
            return this;
        }

        public void Run()
        {
            _applicationServiceProvider = _applicationServices.BuildServiceProvider();
            _infrastructureInitialization?.Invoke(_applicationServiceProvider);
            _applicationServiceProvider.UseServiceHost();
            var messaging = _applicationServiceProvider.GetService<IMessagingInfrastructure>();

            foreach (var subscribeChannelAction in _subscribeChannelActions)
                    subscribeChannelAction(messaging);

            foreach (var testSequenceType in _testSequences)
            {
                _mainLogger.LogInformation($"Starting test sequence \"{testSequenceType.Name}\"...");

                var testSequence = (TestSequenceBase)_applicationServiceProvider.GetService(testSequenceType);

                if (_traceTestSequencesToPath != null)
                {
                    _currentTestSequenceTracer = new TestSequenceTracer(_traceTestSequencesToPath, testSequenceType.Name, testSequence);
                    _tracer.Add(_currentTestSequenceTracer);
                    testSequence.TraceDocumentationAction = _currentTestSequenceTracer.DocumentationTrace;
                    testSequence.TraceTitleAction = _currentTestSequenceTracer.TitleTrace;
                }

                try
                {
                    testSequence.Run();
                    _currentTestSequenceTracer?.TestSequenceSuccessful();
                }
                catch (Exception e)
                {
                    _mainLogger.LogError(e, $"Test sequence \"{testSequenceType.Name}\" failed!");
                    _currentTestSequenceTracer?.TestSequenceFailed(e);

                    if (Debugger.IsAttached)
                        Debugger.Break();
                }
            }
        }

        private static string GetEnvironment()
        {
            var environment = "production";

            var envVarEnvironment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

            if (!string.IsNullOrEmpty(envVarEnvironment))
                environment = envVarEnvironment;

            return environment;
        }

        public void Dispose()
        {
            _applicationServiceProvider?.Dispose();
            foreach(var tracer in _tracer)
                tracer?.Dispose();
        }
    }
}

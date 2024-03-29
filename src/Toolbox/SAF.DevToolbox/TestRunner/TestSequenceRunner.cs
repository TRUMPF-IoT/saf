// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.InProcess;
using SAF.Messaging.Redis;

namespace SAF.DevToolbox.TestRunner;

public class TestSequenceRunner : IDisposable
{
    private readonly ServiceCollection _applicationServices;
    private ServiceProvider? _applicationServiceProvider;

    private readonly List<Type> _testSequences = new();
    private readonly List<TestSequenceTracer> _tracers = new();
    private readonly ILogger<TestSequenceRunner> _mainLogger;
    private readonly IConfigurationRoot _config;
    private readonly List<Action<IMessagingInfrastructure>> _subscribeChannelActions = new();

    private string? _traceTestSequencesToPath;
    private TestSequenceTracer? _currentTestSequenceTracer;

    private Action<IServiceProvider>? _infrastructureInitialization;

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
        _mainLogger = baseServiceProvider.GetRequiredService<ILogger<TestSequenceRunner>>();
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
        _infrastructureInitialization = sp => sp.GetRequiredService<ICdeMessagingInfrastructure>();

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
        _applicationServices.AddTransient<IMessageHandler>(r => r.GetRequiredService<T>());

        _testSequences.Add(typeof(T));

        return this;
    }

    public TestSequenceRunner AddTestMessageHandler<T>(params string[] channels) where T : class, IMessageHandler
    {
        _applicationServices.AddSingleton<T>();
        _applicationServices.AddTransient<IMessageHandler>(r => r.GetRequiredService<T>());

        foreach (var channel in channels)
        {
            _subscribeChannelActions.Add(mi => mi.Subscribe<T>(channel));
        }

        return this;
    }

    public TestSequenceRunner RegisterTestDependencies(IServiceAssemblyManifest testManifest)
    {
        // TODO: Improve handling of tests and test dependencies to be able to apply the correct ServiceHostContext.
        testManifest.RegisterDependencies(_applicationServices, null!);
        return this;
    }

    public void Run()
    {
        _applicationServiceProvider = _applicationServices.BuildServiceProvider();

        _infrastructureInitialization?.Invoke(_applicationServiceProvider);
        _applicationServiceProvider.GetRequiredService<ServiceHost>().StartAsync(CancellationToken.None).Wait();

        var messaging = _applicationServiceProvider.GetRequiredService<IMessagingInfrastructure>();

        foreach (var subscribeChannelAction in _subscribeChannelActions)
            subscribeChannelAction(messaging);

        foreach (var testSequenceType in _testSequences)
        {
            _mainLogger.LogInformation($"Starting test sequence \"{testSequenceType.Name}\"...");

            var testSequence = (TestSequenceBase)_applicationServiceProvider.GetRequiredService(testSequenceType);

            if (_traceTestSequencesToPath != null)
            {
                _currentTestSequenceTracer = new TestSequenceTracer(_traceTestSequencesToPath, testSequenceType.Name, testSequence);
                _tracers.Add(_currentTestSequenceTracer);
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _applicationServiceProvider?.Dispose();
        foreach (var tracer in _tracers)
            tracer?.Dispose();
    }
}
// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests.Diagnostics;
using Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAF.Hosting.Diagnostics;
using System.Diagnostics;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;
using Xunit.Abstractions;

public class ServiceHostDiagnosticsTests
{
    private readonly ILogger<ServiceHostDiagnostics> _logger;
    private readonly IServiceHostInfo _hostInfo = Substitute.For<IServiceHostInfo>();
    private readonly MockFileSystem _fileSystem = new();

    public ServiceHostDiagnosticsTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<ServiceHostDiagnostics>();

        _hostInfo.Id.Returns("hostId");
        _hostInfo.FileSystemUserBasePath.Returns("userbase");
    }

    [Fact]
    public void SafVersionInfoFilledOk()
    {
        var vi = new SafVersionInfo();
        var assembly = Assembly.GetExecutingAssembly();
        var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        var buildNr = assembly.GetName().Version!.ToString();
        Assert.Equal(version, vi.Version);
        Assert.Equal(buildNr, vi.BuildNumber);
    }

    [Fact]
    public void SafServiceInfoFilledOk()
    {
        var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "SAF.Hosting.TestServices.dll"));
        var manifest = loadedAssembly.GetExportedTypes().SingleOrDefault(t => t.IsClass && typeof(IServiceAssemblyManifest).IsAssignableFrom(t));
        Assert.NotNull(manifest);

        var assembly = (IServiceAssemblyManifest)Activator.CreateInstance(manifest)!;
        var si = new SafServiceInfo(assembly);
        Assert.StartsWith("1.2.3.4", si.Version);
        Assert.Equal("1.2.3.4", si.BuildNumber);
    }

    [Fact]
    public async Task StartAsync_CreatesDiagnosticsFile()
    {
        // Arrange
        var serviceAssemblies = new[] { Substitute.For<IServiceAssemblyManifest>() };

        var diagnostics = new ServiceHostDiagnostics(_logger, serviceAssemblies, _hostInfo, _fileSystem);

        // Act
        await diagnostics.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(_fileSystem.Directory.Exists(_fileSystem.Path.Combine("userbase", "diagnostics")));
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine("userbase", "diagnostics", "SafServiceHost_hostId.json")));
    }

    [Fact]
    public async Task StartAsync_ReplacesExistingFile()
    {
        var filePath = _fileSystem.Path.Combine("userbase", "diagnostics", "SafServiceHost_hostId.json");

        _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("userbase", "diagnostics"));
        await _fileSystem.File.WriteAllTextAsync(filePath, "content");

        var serviceAssemblies = new[] { Substitute.For<IServiceAssemblyManifest>() };

        var diagnostics = new ServiceHostDiagnostics(_logger, serviceAssemblies, _hostInfo, _fileSystem);

        await diagnostics.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(_fileSystem.Directory.Exists(_fileSystem.Path.Combine("userbase", "diagnostics")));
        Assert.True(_fileSystem.File.Exists(filePath));
        Assert.NotEqual("content", await _fileSystem.File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task StartAsync_HandlesException()
    {
        var fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        fileSystem.Path.Throws(new InvalidOperationException("dummy"));

        var serviceAssemblies = new[] { Substitute.For<IServiceAssemblyManifest>() };

        var diagnostics = new ServiceHostDiagnostics(_logger, serviceAssemblies, _hostInfo, fileSystem);

        var exception = await Record.ExceptionAsync(() => diagnostics.StartAsync(CancellationToken.None));
        Assert.Null(exception);
    }
}
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Common;
using SAF.Common.Diagnostics;
using SAF.PluginSystem.Hosting.Contracts;
using System.Text.Json;
using Testably.Abstractions.Testing;
using Xunit;

public class ServiceHostDiagnosticsTests
{
    private const string UserBasePath = "/saf/userbase";
    private const string DiagnosticsSubDir = "diagnostics";

    /// <summary>
    /// Builds a <see cref="ServiceHostDiagnostics"/> with sensible defaults.
    /// Any parameter can be overridden per test.
    /// </summary>
    private static ServiceHostDiagnostics CreateSut(
        IServiceHostInfo? hostInfo = null,
        IEnumerable<IPluginAssemblyContainer>? containers = null,
        MockFileSystem? fileSystem = null)
    {
        var services = new ServiceCollection();
        if (hostInfo is not null)
            services.AddSingleton(hostInfo);

        var sp = services.BuildServiceProvider();
        var fs = fileSystem ?? new MockFileSystem();

        return new ServiceHostDiagnostics(
            NullLogger<ServiceHostDiagnostics>.Instance,
            containers ?? [],
            sp,
            fs);
    }

    private static IServiceHostInfo HostInfoWith(string id, string userBasePath)
    {
        var hostInfo = Substitute.For<IServiceHostInfo>();
        hostInfo.Id.Returns(id);
        hostInfo.FileSystemUserBasePath.Returns(userBasePath);
        return hostInfo;
    }

    [Fact]
    public async Task StopAsync_ReturnsCompletedTask()
    {
        var sut = CreateSut();

        await sut.StopAsync(CancellationToken.None);
        // No exception – test passes
    }

    [Fact]
    public async Task StartAsync_WhenHostInfoIsNull_WritesToTempfsUnderBaseDirectory()
    {
        // Arrange
        var fs = new MockFileSystem();
        var sut = CreateSut(hostInfo: null, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert – file must exist somewhere inside <baseDir>/tempfs/diagnostics/
        var expectedDir = fs.Path.Combine(AppContext.BaseDirectory, "tempfs", DiagnosticsSubDir);
        Assert.True(fs.Directory.Exists(expectedDir));
        Assert.Single(fs.Directory.GetFiles(expectedDir, "*.json"));
    }

    [Fact]
    public async Task StartAsync_WhenHostInfoHasUserBasePath_WritesToUserBasePath()
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("host-1", UserBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var expectedDir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        Assert.True(fs.Directory.Exists(expectedDir));
        Assert.Single(fs.Directory.GetFiles(expectedDir, "*.json"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_WhenUserBasePathIsNullOrWhitespace_WritesToTempfs(string userBasePath)
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("host-1", userBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var expectedDir = fs.Path.Combine(AppContext.BaseDirectory, "tempfs", DiagnosticsSubDir);
        Assert.True(fs.Directory.Exists(expectedDir));
    }

    [Fact]
    public async Task StartAsync_FileNameContainsHostId()
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("my-host", UserBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var dir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        var file = fs.Directory.GetFiles(dir).Single();
        Assert.Contains("my-host", fs.Path.GetFileName(file));
    }

    [Fact]
    public async Task StartAsync_InvalidFileNameCharsInHostId_AreReplacedWithUnderscore()
    {
        // Arrange – use a host-id that contains chars invalid on all platforms
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("host/id:with<invalid>chars", UserBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert – all written file names must be valid (no invalid chars remain)
        var dir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        var file = fs.Path.GetFileName(fs.Directory.GetFiles(dir).Single());
        Assert.DoesNotContain('/', file);
        Assert.DoesNotContain(':', file);
        Assert.DoesNotContain('<', file);
        Assert.DoesNotContain('>', file);
    }

    [Fact]
    public async Task StartAsync_WrittenFileContainsValidJson()
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("json-host", UserBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var dir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        var content = fs.File.ReadAllText(fs.Directory.GetFiles(dir).Single());
        var doc = JsonDocument.Parse(content); // throws if invalid JSON
        Assert.Equal("json-host", doc.RootElement.GetProperty("HostId").GetString());
    }

    [Fact]
    public async Task StartAsync_WrittenFileContainsServiceInfoForEachPlugin()
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("plugin-host", UserBasePath);

        // Use the test assembly's own manifest as a real IPluginManifest so SafServiceInfo can reflect it
        var manifest = new TestPluginManifest();
        var container = Substitute.For<IPluginAssemblyContainer>();
        container.GetPluginManifests().Returns([manifest]);

        var sut = CreateSut(hostInfo, [container], fs);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var dir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        var content = fs.File.ReadAllText(fs.Directory.GetFiles(dir).Single());
        var doc = JsonDocument.Parse(content);
        var services = doc.RootElement.GetProperty("SafServices").EnumerateArray().ToList();
        Assert.Single(services);
        Assert.Contains(nameof(TestPluginManifest), services[0].GetProperty("FriendlyName").GetString());
    }

    [Fact]
    public async Task StartAsync_WhenFileAlreadyExists_OverwritesFile()
    {
        // Arrange
        var fs = new MockFileSystem();
        var hostInfo = HostInfoWith("overwrite-host", UserBasePath);
        var sut = CreateSut(hostInfo, fileSystem: fs);

        // Pre-create a stale file at the exact target location
        var dir = fs.Path.Combine(UserBasePath, DiagnosticsSubDir);
        fs.Directory.CreateDirectory(dir);
        var staleFile = fs.Path.Combine(dir, "SafServiceHost_overwrite-host.json");
        fs.File.WriteAllText(staleFile, "stale");

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert – file still exists but content was replaced
        Assert.True(fs.File.Exists(staleFile));
        Assert.NotEqual("stale", fs.File.ReadAllText(staleFile));
    }

    [Fact]
    public async Task StartAsync_WhenCollectionThrows_DoesNotPropagateException()
    {
        // Arrange – container throws during manifest enumeration
        var container = Substitute.For<IPluginAssemblyContainer>();
        container.GetPluginManifests().Returns(_ => throw new InvalidOperationException("boom"));

        var sut = CreateSut(containers: [container]);

        // Act & Assert – must not throw
        await sut.StartAsync(CancellationToken.None);
    }

    private sealed class TestPluginManifest : IPluginManifest
    {
        public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices) { }
    }
}

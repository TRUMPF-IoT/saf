// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq;
using System.Reflection;
using System.IO.Abstractions;
using Testably.Abstractions.Testing;

public class PluginSystemHostBuilderExtensionsTests
{
    private readonly IPluginSystemHostBuilder _hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
    private readonly ServiceCollection _serviceCollection = [];

    public PluginSystemHostBuilderExtensionsTests()
    {
        _serviceCollection.AddTransient(typeof(ILogger<>), typeof(Logger<>));
        _serviceCollection.AddTransient(typeof(ILoggerFactory), _ => Substitute.For<ILoggerFactory>());
        _serviceCollection.AddSingleton<IFileSystem>(new MockFileSystem());
        _serviceCollection.AddSingleton<IPluginManifestLoader, PluginManifestLoader>();

        _hostBuilder.Services.Returns(_serviceCollection);
    }

    [Fact]
    public void AddDigitalSignaturePluginAssemblyValidator_WithConfiguration_ShouldApplyOptions()
    {
        // Act
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator(options =>
        {
            options.RequireValidDigitalSignature = true;
            options.AllowedSignerThumbprints.Add("0011223344556677");
        });

        // Assert
        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var validator = Assert.IsType<DigitalSignaturePluginAssemblyValidator>(
            Assert.Single(serviceProvider.GetServices<IPluginAssemblyValidator>()));

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var context = new PluginAssemblyValidationContext(assemblyPath, AssemblyName.GetAssemblyName(assemblyPath));
        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
    }

    [Fact]
    public void AddStrongNamePluginAssemblyValidator_WithConfiguration_ShouldApplyOptions()
    {
        // Act
        _hostBuilder.AddStrongNamePluginAssemblyValidator(options =>
        {
            options.RequireStrongName = true;
            options.AllowedPublicKeyTokens.Add("0011223344556677");
        });

        // Assert
        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var validator = Assert.IsType<StrongNamePluginAssemblyValidator>(
            Assert.Single(serviceProvider.GetServices<IPluginAssemblyValidator>()));

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var context = new PluginAssemblyValidationContext(assemblyPath, AssemblyName.GetAssemblyName(assemblyPath));
        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
    }

    [Fact]
    public void AddPluginAssemblyValidator_ShouldPreserveRegistrationOrder()
    {
        _hostBuilder.AddPluginAssemblyValidator<FirstValidator>();
        _hostBuilder.AddStrongNamePluginAssemblyValidator();
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator();
        _hostBuilder.AddPluginAssemblyValidator<SecondValidator>();

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var validatorTypes = serviceProvider.GetServices<IPluginAssemblyValidator>().Select(v => v.GetType()).ToList();

        Assert.Equal(typeof(FirstValidator), validatorTypes[0]);
        Assert.Equal(typeof(StrongNamePluginAssemblyValidator), validatorTypes[1]);
        Assert.Equal(typeof(DigitalSignaturePluginAssemblyValidator), validatorTypes[2]);
        Assert.Equal(typeof(SecondValidator), validatorTypes[3]);
    }

    private sealed class FirstValidator : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => PluginAssemblyValidationResult.Accepted();
    }

    private sealed class SecondValidator : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => PluginAssemblyValidationResult.Accepted();
    }
}
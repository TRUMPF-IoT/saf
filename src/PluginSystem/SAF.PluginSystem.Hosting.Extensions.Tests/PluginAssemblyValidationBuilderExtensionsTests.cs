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
using SAF.PluginSystem.Hosting.Extensions;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

public class PluginAssemblyValidationBuilderExtensionsTests
{
    private readonly IPluginSystemHostBuilder _hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
    private readonly ServiceCollection _serviceCollection = [];

    public PluginAssemblyValidationBuilderExtensionsTests()
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
    public void AddStrongNamePluginAssemblyValidator_MultipleRegistrations_ShouldKeepOptionsIsolated()
    {
        var assemblyName = typeof(object).Assembly.GetName();
        var publicKeyToken = Convert.ToHexString(assemblyName.GetPublicKeyToken()!).ToLowerInvariant();

        _hostBuilder.AddStrongNamePluginAssemblyValidator(options =>
            options.AllowedPublicKeyTokens.Add(publicKeyToken));
        _hostBuilder.AddStrongNamePluginAssemblyValidator(options =>
            options.AllowedPublicKeyTokens.Add("0011223344556677"));

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var validators = serviceProvider.GetServices<IPluginAssemblyValidator>()
            .Cast<StrongNamePluginAssemblyValidator>()
            .ToList();
        var context = new PluginAssemblyValidationContext("dummy.dll", assemblyName);

        Assert.Equal(2, validators.Count);
        Assert.True(validators[0].Validate(context).IsAccepted);
        Assert.False(validators[1].Validate(context).IsAccepted);
    }

    [Fact]
    public void AddDigitalSignaturePluginAssemblyValidator_MultipleRegistrations_ShouldKeepOptionsIsolated()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var context = new PluginAssemblyValidationContext(assemblyPath, AssemblyName.GetAssemblyName(assemblyPath));

        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator();
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator(options =>
            options.AllowedSignerThumbprints.Add("00112233445566778899AABBCCDDEEFF00112233"));

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var validators = serviceProvider.GetServices<IPluginAssemblyValidator>()
            .Cast<DigitalSignaturePluginAssemblyValidator>()
            .ToList();

        Assert.Equal(2, validators.Count);
        Assert.True(validators[0].Validate(context).IsAccepted);
        Assert.False(validators[1].Validate(context).IsAccepted);
    }

    [Fact]
    public void AddDigitalSignaturePluginAssemblyValidator_MultipleRegistrations_ShouldShareTheAuthenticodeServices()
    {
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator();
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator(options =>
            options.AllowedSignerThumbprints.Add("00112233445566778899AABBCCDDEEFF00112233"));

        // A second trust domain is a second validator, not a second copy of the machinery behind it.
        Assert.Equal(2, CountRegistrations<IPluginAssemblyValidator>());
        Assert.Equal(1, CountRegistrations<IAuthenticodeCertificateTableParser>());
        Assert.Equal(1, CountRegistrations<IAuthenticodeChainTrustVerifier>());
        Assert.Equal(1, CountRegistrations<IAuthenticodePeHasher>());
        Assert.Equal(1, CountRegistrations<IAuthenticodeSignatureReader>());
    }

    [Fact]
    public void AddDigitalSignaturePluginAssemblyValidator_ShouldRegisterAResolvableSignatureReader()
    {
        _hostBuilder.AddDigitalSignaturePluginAssemblyValidator();

        var serviceProvider = _serviceCollection.BuildServiceProvider();

        // The registration is the only place the reader's graph is assembled, so it has to be complete.
        Assert.NotNull(serviceProvider.GetRequiredService<IAuthenticodeSignatureReader>());
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

    private int CountRegistrations<TService>()
        => _serviceCollection.Count(descriptor => descriptor.ServiceType == typeof(TService));

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
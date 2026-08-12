// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Extensions;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Reflection;

public class DigitalSignaturePluginAssemblyValidatorTests
{
    private const string AssemblyPath = "any-plugin.dll";

    private readonly IAuthenticodeSignatureReader _signatureReader = Substitute.For<IAuthenticodeSignatureReader>();

    [Fact]
    public void Validate_Throws_WhenContextIsNull()
    {
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions());

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Validate_Accepts_WhenNoSignatureChecksAreConfigured()
    {
        _signatureReader.ReadSignature(AssemblyPath).Returns((AuthenticodeSignatureInfo?)null);
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions());

        var result = validator.Validate(CreateContext());

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenSignatureIsRequiredAndFileHasNoSignature()
    {
        _signatureReader.ReadSignature(AssemblyPath).Returns((AuthenticodeSignatureInfo?)null);
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = true });

        var result = validator.Validate(CreateContext());

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly does not have a valid digital signature", result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenSignatureIsRequiredButNotTrusted()
    {
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("AABBCC", HasValidDigitalSignature: false));
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = true });

        var result = validator.Validate(CreateContext());

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly does not have a valid digital signature", result.Reason);
    }

    [Fact]
    public void Validate_Accepts_WhenSignatureIsRequiredAndValid()
    {
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("AABBCC", HasValidDigitalSignature: true));
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = true });

        var result = validator.Validate(CreateContext());

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenSignerThumbprintIsNotInAllowList()
    {
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("112233", HasValidDigitalSignature: true));

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        options.AllowedSignerThumbprints.Add("AABBCCDDEEFF00112233445566778899AABBCCDD");
        var validator = CreateValidator(options);

        var result = validator.Validate(CreateContext());

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly signer thumbprint is not in the configured allow-list", result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenAllowListIsConfiguredButSignatureDoesNotCoverFile()
    {
        // A transplanted or tampered signature yields no trustworthy thumbprint.
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo(SignerThumbprint: null, HasValidDigitalSignature: false));

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        options.AllowedSignerThumbprints.Add("AABBCC");
        var validator = CreateValidator(options);

        var result = validator.Validate(CreateContext());

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly signer thumbprint is not in the configured allow-list", result.Reason);
    }

    [Fact]
    public void Validate_Accepts_WhenSignerThumbprintIsInAllowList_IgnoringCase()
    {
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("aabbccddeeff", HasValidDigitalSignature: true));

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        options.AllowedSignerThumbprints.Add("AABBCCDDEEFF");
        var validator = CreateValidator(options);

        var result = validator.Validate(CreateContext());

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_ReadsTheFile_WhenAPathIsAvailable()
    {
        var assemblyBytes = new byte[] { 0x01, 0x02, 0x03 };
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("AABBCC", HasValidDigitalSignature: true));
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions
        {
            RequireValidDigitalSignature = true
        });
        var context = new PluginAssemblyValidationContext(
            AssemblyPath,
            new AssemblyName("AnyAssembly"),
            assemblyBytes);

        var result = validator.Validate(context);

        // The hosting pipeline rejects a candidate whose file diverged from the snapshot, so reading the
        // file is safe and spares a path-based verifier the temporary copy of the snapshot.
        Assert.True(result.IsAccepted);
        _signatureReader.Received(1).ReadSignature(AssemblyPath);
        _signatureReader.DidNotReceive().ReadSignature(Arg.Any<ReadOnlyMemory<byte>>());
    }

    [Fact]
    public void Validate_UsesContentSnapshot_WhenNoPathIsAvailable()
    {
        var assemblyBytes = new byte[] { 0x01, 0x02, 0x03 };
        _signatureReader.ReadSignature(Arg.Any<ReadOnlyMemory<byte>>())
            .Returns(new AuthenticodeSignatureInfo("AABBCC", HasValidDigitalSignature: true));
        var validator = CreateValidator(new DigitalSignaturePluginAssemblyValidatorOptions
        {
            RequireValidDigitalSignature = true
        });
        var context = new PluginAssemblyValidationContext(
            string.Empty,
            new AssemblyName("AnyAssembly"),
            assemblyBytes);

        var result = validator.Validate(context);

        Assert.True(result.IsAccepted);
        _signatureReader.Received(1).ReadSignature(
            Arg.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray().SequenceEqual(assemblyBytes)));
    }

    [Fact]
    public void Validate_UsesCurrentOptionsFromMonitor()
    {
        _signatureReader.ReadSignature(AssemblyPath)
            .Returns(new AuthenticodeSignatureInfo("AABBCC", HasValidDigitalSignature: true));

        var currentOptions = new DigitalSignaturePluginAssemblyValidatorOptions();
        currentOptions.AllowedSignerThumbprints.Add("AABBCC");
        var optionsMonitor = Substitute.For<IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions>>();
        optionsMonitor.Get(Arg.Any<string>()).Returns(_ => currentOptions);
        var validator = new DigitalSignaturePluginAssemblyValidator(
            optionsMonitor,
            Options.DefaultName,
            _signatureReader);

        var initialResult = validator.Validate(CreateContext());

        currentOptions = new DigitalSignaturePluginAssemblyValidatorOptions();
        currentOptions.AllowedSignerThumbprints.Add("DDEEFF");
        var updatedResult = validator.Validate(CreateContext());

        Assert.True(initialResult.IsAccepted);
        Assert.False(updatedResult.IsAccepted);
    }

    private DigitalSignaturePluginAssemblyValidator CreateValidator(DigitalSignaturePluginAssemblyValidatorOptions options)
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions>>();
        optionsMonitor.Get(Options.DefaultName).Returns(options);
        return new(optionsMonitor, Options.DefaultName, _signatureReader);
    }

    private static PluginAssemblyValidationContext CreateContext()
        => new(AssemblyPath, new AssemblyName("AnyAssembly"));
}

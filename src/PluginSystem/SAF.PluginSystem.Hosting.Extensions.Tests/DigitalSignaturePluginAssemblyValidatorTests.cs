// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using NSubstitute;
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

    private DigitalSignaturePluginAssemblyValidator CreateValidator(DigitalSignaturePluginAssemblyValidatorOptions options)
        => new(options, _signatureReader);

    private static PluginAssemblyValidationContext CreateContext()
        => new(AssemblyPath, new AssemblyName("AnyAssembly"));
}

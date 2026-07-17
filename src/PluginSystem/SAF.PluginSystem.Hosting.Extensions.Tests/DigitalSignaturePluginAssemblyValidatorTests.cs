// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using SAF.PluginSystem.Hosting.Extensions;
using System.Reflection;

public class DigitalSignaturePluginAssemblyValidatorTests
{
    private readonly AuthenticodeSignatureReader _authenticodeSignatureReader = new();

    [Fact]
    public void Validate_Throws_WhenContextIsNull()
    {
        var validator = new DigitalSignaturePluginAssemblyValidator(new DigitalSignaturePluginAssemblyValidatorOptions(), _authenticodeSignatureReader);

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Validate_Accepts_WhenNoSignatureChecksAreConfigured()
    {
        var validator = new DigitalSignaturePluginAssemblyValidator(new DigitalSignaturePluginAssemblyValidatorOptions(), _authenticodeSignatureReader);
        var context = new PluginAssemblyValidationContext("missing-file.dll", new AssemblyName("AnyAssembly"));

        var result = validator.Validate(context);

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenSignatureIsRequiredAndFileHasNoValidSignature()
    {
        var options = new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = true };
        var validator = new DigitalSignaturePluginAssemblyValidator(options, _authenticodeSignatureReader);
        var context = new PluginAssemblyValidationContext("missing-file.dll", new AssemblyName("AnyAssembly"));

        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly does not have a valid digital signature", result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenSignerThumbprintIsNotInAllowList()
    {
        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        options.AllowedSignerThumbprints.Add("AABBCCDDEEFF00112233445566778899AABBCCDD");

        var validator = new DigitalSignaturePluginAssemblyValidator(options, _authenticodeSignatureReader);
        var context = new PluginAssemblyValidationContext("missing-file.dll", new AssemblyName("AnyAssembly"));

        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly signer thumbprint is not in the configured allow-list", result.Reason);
    }

    [Fact]
    public void Validate_Accepts_WhenSignerThumbprintIsInAllowList()
    {
        var signedAssemblyPath = FindSignedAssemblyPathWithThumbprint(out var signerThumbprint);
        Assert.False(string.IsNullOrWhiteSpace(signedAssemblyPath));
        Assert.False(string.IsNullOrWhiteSpace(signerThumbprint));

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        options.AllowedSignerThumbprints.Add(signerThumbprint!);

        var validator = new DigitalSignaturePluginAssemblyValidator(options, _authenticodeSignatureReader);
        var context = new PluginAssemblyValidationContext(signedAssemblyPath!, new AssemblyName("SignedAssembly"));

        var result = validator.Validate(context);

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }

    private string? FindSignedAssemblyPathWithThumbprint(out string? signerThumbprint)
    {
        signerThumbprint = null;
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return null;
        }

        foreach (var candidatePath in Directory.EnumerateFiles(runtimeDirectory, "*.dll"))
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(candidatePath);
                _ = assemblyName.FullName;

                if (!_authenticodeSignatureReader.TryGetSignatureInfo(candidatePath, out signerThumbprint, out _)
                    || string.IsNullOrWhiteSpace(signerThumbprint))
                {
                    continue;
                }

                return candidatePath;
            }
            catch
            {
                // continue with next candidate
            }
        }

        return null;
    }
}

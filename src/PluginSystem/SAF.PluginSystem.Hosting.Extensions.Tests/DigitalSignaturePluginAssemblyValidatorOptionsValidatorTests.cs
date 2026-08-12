// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using SAF.PluginSystem.Hosting.Extensions;

public class DigitalSignaturePluginAssemblyValidatorOptionsValidatorTests
{
    private readonly DigitalSignaturePluginAssemblyValidatorOptionsValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_ForTheDefaultOptions()
    {
        var result = _validator.Validate(name: null, new DigitalSignaturePluginAssemblyValidatorOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyAnAllowListIsConfigured()
    {
        var options = new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = false };
        options.AllowedSignerThumbprints.Add("AABBCCDDEEFF00112233445566778899AABBCCDD");

        // Still demands a signature that covers the file - it only skips the trust chain.
        Assert.True(_validator.Validate(name: null, options).Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenEveryCheckIsSwitchedOff()
    {
        var options = new DigitalSignaturePluginAssemblyValidatorOptions { RequireValidDigitalSignature = false };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(nameof(DigitalSignaturePluginAssemblyValidatorOptions.RequireValidDigitalSignature), result.FailureMessage);
        Assert.Contains(nameof(DigitalSignaturePluginAssemblyValidatorOptions.AllowedSignerThumbprints), result.FailureMessage);
    }

    [Fact]
    public void Validate_Throws_WhenOptionsAreNull()
        => Assert.Throws<ArgumentNullException>(() => _validator.Validate(name: null, null!));
}

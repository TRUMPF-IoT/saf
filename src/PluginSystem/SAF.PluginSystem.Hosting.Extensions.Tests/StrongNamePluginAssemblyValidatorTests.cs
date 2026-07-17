// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using System.Reflection;

public class StrongNamePluginAssemblyValidatorTests
{
    [Fact]
    public void Validate_Throws_WhenContextIsNull()
    {
        var validator = new StrongNamePluginAssemblyValidator(new StrongNamePluginAssemblyValidatorOptions());

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Validate_Accepts_WhenNoStrongNameChecksAreConfigured()
    {
        var validator = new StrongNamePluginAssemblyValidator(new StrongNamePluginAssemblyValidatorOptions());
        var context = new PluginAssemblyValidationContext("dummy.dll", new AssemblyName("UnsignedAssembly"));

        var result = validator.Validate(context);

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);

    }
    [Fact]
    public void Validate_Rejects_WhenStrongNameIsRequiredAndAssemblyIsUnsigned()
    {
        var options = new StrongNamePluginAssemblyValidatorOptions { RequireStrongName = true };
        var validator = new StrongNamePluginAssemblyValidator(options);
        var context = new PluginAssemblyValidationContext("dummy.dll", new AssemblyName("UnsignedAssembly"));

        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly is not strong-name signed", result.Reason);
    }

    [Fact]
    public void Validate_Rejects_WhenPublicKeyTokenIsNotInAllowList()
    {
        var options = new StrongNamePluginAssemblyValidatorOptions();
        options.AllowedPublicKeyTokens.Add("0011223344556677");

        var validator = new StrongNamePluginAssemblyValidator(options);
        var context = new PluginAssemblyValidationContext("dummy.dll", typeof(object).Assembly.GetName());

        var result = validator.Validate(context);

        Assert.False(result.IsAccepted);
        Assert.Equal("assembly public key token is not in the configured allow-list", result.Reason);
    }

    [Fact]
    public void Validate_Accepts_WhenPublicKeyTokenIsInAllowList()
    {
        var assemblyName = typeof(object).Assembly.GetName();
        var token = Convert.ToHexString(assemblyName.GetPublicKeyToken()!).ToLowerInvariant();

        var options = new StrongNamePluginAssemblyValidatorOptions();
        options.AllowedPublicKeyTokens.Add(token);

        var validator = new StrongNamePluginAssemblyValidator(options);
        var context = new PluginAssemblyValidationContext("dummy.dll", assemblyName);

        var result = validator.Validate(context);

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
    }
}

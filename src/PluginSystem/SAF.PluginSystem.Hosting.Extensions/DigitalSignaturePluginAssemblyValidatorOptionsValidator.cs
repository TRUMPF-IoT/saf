// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Microsoft.Extensions.Options;

/// <summary>
/// Refuses a digital-signature validator whose every check is switched off.
/// </summary>
/// <remarks>
/// Such a validator accepts every candidate, including an unsigned one, while the composition root still
/// reads as protection - which is worse than registering no validator at all. Failing here turns that into
/// a startup error instead of a silent hole.
/// </remarks>
internal sealed class DigitalSignaturePluginAssemblyValidatorOptionsValidator
    : IValidateOptions<DigitalSignaturePluginAssemblyValidatorOptions>
{
    public ValidateOptionsResult Validate(string? name, DigitalSignaturePluginAssemblyValidatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RequireValidDigitalSignature || options.AllowedSignerThumbprints.Count > 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{nameof(DigitalSignaturePluginAssemblyValidatorOptions.RequireValidDigitalSignature)} is disabled and " +
            $"{nameof(DigitalSignaturePluginAssemblyValidatorOptions.AllowedSignerThumbprints)} is empty, so this " +
            "digital-signature validator would accept every plugin assembly. Require a valid signature, list the " +
            "signer thumbprints you trust, or do not register the validator.");
    }
}

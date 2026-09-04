// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Extensions;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// What the secret store host-builder calls have already decided, kept in the service collection so a
/// later call can see it. Provider registration order is the priority order auto-selection uses, and it
/// can only be decided once - the second opinion would either be silently ignored or silently win.
/// </summary>
internal sealed class SecretStoreRegistration
{
    /// <summary>The call that fixed the provider order, or <see langword="null"/> while none has.</summary>
    public string? ProvidersConfiguredBy { get; set; }

    /// <summary>Whether configuration resolution is already registered; a second one would resolve twice.</summary>
    public bool ResolutionRegistered { get; set; }

    public static SecretStoreRegistration GetOrAdd(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(SecretStoreRegistration))
            ?.ImplementationInstance;
        if (existing is SecretStoreRegistration registration)
        {
            return registration;
        }

        registration = new SecretStoreRegistration();
        services.AddSingleton(registration);
        return registration;
    }
}

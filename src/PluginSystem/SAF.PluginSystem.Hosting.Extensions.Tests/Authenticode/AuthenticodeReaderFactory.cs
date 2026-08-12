// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

/// <summary>
/// Builds signature readers for tests without spelling out the object graph at every call site.
/// </summary>
internal static class AuthenticodeReaderFactory
{
    /// <summary>
    /// Resolves a reader from the production service registration, so tests run against the graph a host
    /// really gets - including the platform-dependent choice of trust verifier.
    /// </summary>
    public static IAuthenticodeSignatureReader CreateDefault()
    {
        var services = new ServiceCollection();
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);

        hostBuilder.AddDigitalSignaturePluginAssemblyValidator();

        return services.BuildServiceProvider().GetRequiredService<IAuthenticodeSignatureReader>();
    }

    /// <summary>
    /// Builds a reader around a trust verifier of the test's choosing, which is the one collaborator the
    /// registration cannot supply: it picks the verifier from the operating system.
    /// </summary>
    public static IAuthenticodeSignatureReader Create(IAuthenticodeChainTrustVerifier trustVerifier)
    {
        var certificateTableParser = new AuthenticodeCertificateTableParser();
        return new AuthenticodeSignatureReader(
            trustVerifier,
            new AuthenticodePeHasher(certificateTableParser),
            certificateTableParser);
    }
}

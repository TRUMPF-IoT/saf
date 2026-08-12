// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

/// <summary>
/// Locates the Authenticode fixtures the integration tests run against, and decides whether a missing one
/// is a skip or a failure.
/// </summary>
/// <remarks>
/// Those tests all begin with a skip, so an environment without fixtures reports green having asserted
/// nothing. That is the right answer on a developer machine, where a signtool signature cannot be produced.
/// It is the wrong answer in the CI jobs that build and download the fixture first: there a skip means the
/// setup broke, and the suite would keep passing while nothing was verified. Those jobs set
/// <c>SAF_AUTHENTICODE_REQUIRE_FIXTURE</c>, which turns every missing fixture into a failure.
/// </remarks>
internal static class AuthenticodeFixtures
{
    private const string SignedAssemblyEnvironmentVariable = "SAF_AUTHENTICODE_SIGNED_ASSEMBLY";
    private const string TrustedRootEnvironmentVariable = "SAF_AUTHENTICODE_TRUSTED_ROOT";
    private const string RequireFixtureEnvironmentVariable = "SAF_AUTHENTICODE_REQUIRE_FIXTURE";

    /// <summary>
    /// The assembly signed by signtool in the fixture job, whose certificate no host trusts.
    /// </summary>
    public static string? FindSigntoolSignedAssembly()
    {
        var path = Environment.GetEnvironmentVariable(SignedAssemblyEnvironmentVariable);
        return RequiredInCi(
            !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null,
            $"{SignedAssemblyEnvironmentVariable} does not point at an existing file.");
    }

    /// <summary>
    /// The same fixture, once the job has installed its certificate as a trusted root.
    /// </summary>
    /// <remarks>
    /// Gated on <c>SAF_AUTHENTICODE_TRUSTED_ROOT</c> rather than on the require flag: installing a root is
    /// a machine-wide change, so only the job that made it may claim the tests must run. Once it does, a
    /// missing fixture is a failure there too.
    /// </remarks>
    public static string? FindTrustedSigntoolSignedAssembly()
    {
        if (Environment.GetEnvironmentVariable(TrustedRootEnvironmentVariable) != "1")
        {
            return null;
        }

        var path = Environment.GetEnvironmentVariable(SignedAssemblyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return path;
        }

        throw new InvalidOperationException(
            $"{TrustedRootEnvironmentVariable} is set, so these tests must not be skipped: " +
            $"{SignedAssemblyEnvironmentVariable} does not point at an existing file.");
    }

    /// <summary>
    /// A .NET runtime assembly whose signature verifiably covers the file. Whether it also chains to a
    /// trusted root depends on the platform trust store, so callers must not assume it.
    /// </summary>
    public static string? FindSignedDotNetRuntimeAssembly()
        => RequiredInCi(
            DiscoverSignedDotNetRuntimeAssembly(),
            "no Authenticode-signed assembly was found in the .NET runtime directory.");

    private static string? DiscoverSignedDotNetRuntimeAssembly()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (string.IsNullOrWhiteSpace(runtimeDirectory) || !Directory.Exists(runtimeDirectory))
        {
            return null;
        }

        var reader = AuthenticodeReaderFactory.CreateDefault();
        foreach (var candidate in Directory.EnumerateFiles(runtimeDirectory, "*.dll"))
        {
            if (reader.ReadSignature(candidate) is { SignerThumbprint: { Length: > 0 } })
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? RequiredInCi(string? fixturePath, string reason)
    {
        if (fixturePath is not null ||
            Environment.GetEnvironmentVariable(RequireFixtureEnvironmentVariable) != "1")
        {
            return fixturePath;
        }

        throw new InvalidOperationException(
            $"{RequireFixtureEnvironmentVariable} is set, so this test must not be skipped: {reason}");
    }
}

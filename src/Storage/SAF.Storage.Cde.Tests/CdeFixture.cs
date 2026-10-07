// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.Cde.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Cde.Common;

/// <summary>
/// Holds a lease on the C-DEngine node of the test process for the tests of the <see cref="CdeCollection"/>.
/// </summary>
/// <remarks>
/// C-DEngine starts once per process, and releasing the last lease shuts it down for good. The lease of this
/// fixture keeps the node running while the tests build and dispose plug-in containers on it.
/// </remarks>
public sealed class CdeFixture : IDisposable
{
    private readonly ServiceProvider _services;
    private Storage? _storage;

    public CdeFixture()
    {
        HostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cde:ScopeId"] = "12345678",
                ["Cde:ApplicationId"] = "/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?",
                ["Cde:StorageId"] = "{DE4E9E30-1241-4E85-B5FC-1606910F0709}",
                ["Cde:ApplicationName"] = "SAF Tests",
                ["Cde:ApplicationTitle"] = "SAF Tests",
                ["Cde:PortalTitle"] = "SAF Tests",
                ["Cde:ApplicationVersion"] = "1.0001",
                ["Cde:HttpPort"] = "8080",
                ["Cde:WsPort"] = "8080",
                ["Cde:AllowLocalHost"] = "true",
                ["Cde:DontVerifyTrust"] = "true"
            })
            .Build();

        _services = new ServiceCollection()
            .AddCde(c => HostConfiguration.GetSection("Cde").Bind(c))
            .BuildServiceProvider();
        _ = _services.GetRequiredService<CdeNodeLease>();
    }

    /// <summary>
    /// The host configuration the node was started with.
    /// </summary>
    public IConfiguration HostConfiguration { get; }

    internal Storage Storage => _storage ??= new Storage(null);

    public void Dispose()
    {
        _storage?.Dispose();
        _services.Dispose();
    }
}
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Tests;
using Xunit;

public class AppDomainCdeNodeOwnershipTests
{
    // Each test gets its own marker key, so the tests never see each other's claims.
    private readonly string _markerKey = $"SAF.Cde.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void Claim_Succeeds_WhenNoCopyRunsCde()
    {
        var ownership = new AppDomainCdeNodeOwnership(_markerKey);

        ownership.Claim();

        Assert.NotNull(AppDomain.CurrentDomain.GetData(_markerKey));
    }

    [Fact]
    public void Claim_Succeeds_WhenThisCopyClaimsAgain()
    {
        var ownership = new AppDomainCdeNodeOwnership(_markerKey);
        ownership.Claim();

        ownership.Claim();
    }

    [Fact]
    public void Claim_Throws_WhenAnotherCopyRunsCde()
    {
        // A second instance on the same marker key stands in for a copy of SAF.Cde in another load context.
        new AppDomainCdeNodeOwnership(_markerKey).Claim();
        var otherCopy = new AppDomainCdeNodeOwnership(_markerKey);

        var exception = Assert.Throws<InvalidOperationException>(otherCopy.Claim);

        Assert.Contains("base directory", exception.Message, StringComparison.Ordinal);
    }
}
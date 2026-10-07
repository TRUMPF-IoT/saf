// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common.Tests;
using Xunit;

public class AppDomainCdeNodeOwnershipTests
{
    private const int ConcurrentRounds = 100;
    private const int ConcurrentCopies = 8;

    // Each test gets its own marker key, so the tests never see each other's claims.
    private readonly string _markerKey = $"SAF.Cde.Common.Tests.{Guid.NewGuid():N}";

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
        // A second instance on the same marker key stands in for a copy of SAF.Cde.Common in another load context.
        new AppDomainCdeNodeOwnership(_markerKey).Claim();
        var otherCopy = new AppDomainCdeNodeOwnership(_markerKey);

        var exception = Assert.Throws<InvalidOperationException>(otherCopy.Claim);

        Assert.Contains("base directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Claim_LetsExactlyOneCopyWin_WhenCopiesClaimAtTheSameTime()
    {
        // A race shows up only now and then, so the copies race in many rounds, each on a fresh marker key.
        for (var round = 0; round < ConcurrentRounds; round++)
        {
            var markerKey = $"{_markerKey}.{round}";
            using var barrier = new Barrier(ConcurrentCopies);
            var winners = 0;

            var threads = Enumerable.Range(0, ConcurrentCopies)
                .Select(_ => new AppDomainCdeNodeOwnership(markerKey))
                .Select(copy => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        copy.Claim();
                        Interlocked.Increment(ref winners);
                    }
                    catch (InvalidOperationException)
                    {
                        // Another copy claimed first.
                    }
                }))
                .ToArray();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(1, winners);
        }
    }
}
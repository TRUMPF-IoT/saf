// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common.Tests;
using Xunit;

public class CdeNodeLeaseTests
{
    [Fact]
    public void Dispose_ReleasesTheLeaseOnlyOnce()
    {
        var releaseCount = 0;
        var lease = new CdeNodeLease(() => releaseCount++);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, releaseCount);
    }
}
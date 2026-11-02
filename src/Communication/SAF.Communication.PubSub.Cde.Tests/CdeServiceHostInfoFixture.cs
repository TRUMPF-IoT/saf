// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

[assembly: Xunit.AssemblyFixture(typeof(SAF.Communication.PubSub.Cde.Tests.CdeServiceHostInfoFixture))]

namespace SAF.Communication.PubSub.Cde.Tests;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;

/// <summary>
/// Makes C-DEngine read JSON strictly, as a SAF node does by default. C-DEngine takes the option from
/// <see cref="TheBaseAssets.MyServiceHostInfo"/>, which only a started node sets, and reads leniently without it;
/// these tests use C-DEngine's serializer without a node.
/// </summary>
public sealed class CdeServiceHostInfoFixture : IDisposable
{
    private readonly TheServiceHostInfo? _previous = TheBaseAssets.MyServiceHostInfo;

    public CdeServiceHostInfoFixture()
        => TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application) { UseStrictJSON = true };

    public void Dispose() => TheBaseAssets.MyServiceHostInfo = _previous;
}

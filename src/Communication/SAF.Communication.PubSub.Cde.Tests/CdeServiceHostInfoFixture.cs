// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

[assembly: Xunit.AssemblyFixture(typeof(SAF.Communication.PubSub.Cde.Tests.CdeServiceHostInfoFixture))]

namespace SAF.Communication.PubSub.Cde.Tests;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;

/// <summary>
/// C-DEngine 7 reads its JSON options from <see cref="TheBaseAssets.MyServiceHostInfo"/> and throws a
/// <see cref="NullReferenceException"/> without it. A started node sets it; these tests use C-DEngine's serializer
/// without a node, so they get a host info of their own. It reads JSON strictly, as a SAF node does by default.
/// </summary>
public sealed class CdeServiceHostInfoFixture : IDisposable
{
    private readonly TheServiceHostInfo? _previous = TheBaseAssets.MyServiceHostInfo;

    public CdeServiceHostInfoFixture()
        => TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application) { UseStrictJSON = true };

    public void Dispose() => TheBaseAssets.MyServiceHostInfo = _previous;
}

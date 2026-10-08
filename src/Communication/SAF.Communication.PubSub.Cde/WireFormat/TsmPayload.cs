// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using nsCDEngine.BaseClasses;

/// <summary>
/// The payload fields of a TSM: <see cref="TSM.PLS"/> and <see cref="TSM.PLB"/>.
/// </summary>
internal readonly record struct TsmPayload(string? Pls, byte[]? Plb = null)
{
    public static TsmPayload Of(TSM tsm) => new(tsm.PLS, tsm.PLB);

    public TSM ToTsm(string engine, string txt) => new(engine, txt, Pls) { PLB = Plb };
}

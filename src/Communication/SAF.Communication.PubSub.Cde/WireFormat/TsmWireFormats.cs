// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

/// <summary>
/// The message formats this SAF version supports. Register a new format here.
/// </summary>
internal static class TsmWireFormats
{
    public static IReadOnlyList<ITsmMessageFormat> All { get; } = [new TsmMessageFormatV1(), new TsmMessageFormatV2(), new TsmMessageFormatV5(new TsmPlbLayout())];

    public static TsmMessageCodec CreateCodec() => new(All);
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using Interfaces;

/// <summary>
/// The message formats this SAF version supports. Register a new format here.
/// </summary>
internal static class TsmWireFormats
{
    public static IReadOnlyList<ITsmMessageFormat> All { get; } = CreateFormats();

    public static TsmMessageCodec CreateCodec() => new(All);

    private static IReadOnlyList<ITsmMessageFormat> CreateFormats()
    {
        var v5 = new TsmMessageFormatV5(new TsmPlbLayout());
        // Versions 2 to 4 send the JSON of version 5, without binary payloads; version 4 adds batches.
        return [new TsmMessageFormatV1(), new TsmTextOnlyFormat(v5, Version.Parse(PubSubVersion.V2)), v5];
    }
}

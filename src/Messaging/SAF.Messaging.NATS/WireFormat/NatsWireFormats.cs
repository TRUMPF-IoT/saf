// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

/// <summary>
/// The wire formats this SAF version supports. Register a new format here.
/// </summary>
internal static class NatsWireFormats
{
    private static readonly INatsMetadataHeader MetadataHeader = new NatsMetadataHeader();

    public static IReadOnlyList<INatsWireFormat> All { get; } =
        [new NatsV1Format(), new NatsV2Format(MetadataHeader), new NatsV3Format(MetadataHeader)];
}

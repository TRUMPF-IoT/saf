// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

/// <summary>
/// The wire formats this SAF version supports. Register a new format here.
/// </summary>
internal static class NatsWireFormats
{
    public static IReadOnlyList<INatsWireFormat> All { get; } = [new NatsV1Format(), new NatsV2Format()];
}

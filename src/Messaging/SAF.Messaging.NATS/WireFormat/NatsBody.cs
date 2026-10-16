// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Text;

/// <summary>
/// A received body. It is valid while the message is read only; a format copies what it keeps.
/// </summary>
internal readonly struct NatsBody(ReadOnlyMemory<byte> bytes)
{
    /// <summary>
    /// Reads the body exactly like NATS.Net reads it into a <see cref="string"/>, which SAF did up to 11.0.0-alpha.9.
    /// </summary>
    // NATS delivers an empty body as null, whether "" or null was sent.
    public string? ReadText() => bytes.IsEmpty ? null : Encoding.UTF8.GetString(bytes.Span);

    public byte[] ToArray() => bytes.ToArray();
}

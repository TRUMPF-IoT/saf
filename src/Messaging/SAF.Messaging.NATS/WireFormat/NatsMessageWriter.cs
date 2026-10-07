// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Writes each message in the oldest format that can carry it, so older nodes and servers can read as much as possible.
/// </summary>
internal sealed class NatsMessageWriter : INatsMessageWriter
{
    private readonly IReadOnlyList<INatsWireFormat> _formats;

    public NatsMessageWriter(IEnumerable<INatsWireFormat> formats)
    {
        _formats = formats.OrderBy(f => f.MajorVersion).ToList();
    }

    public bool TryWrite(Message message, out NatsWireMessage wireMessage)
    {
        var format = _formats.FirstOrDefault(f => f.CanWrite(message));
        wireMessage = format?.Write(message) ?? default;
        return format != null;
    }
}

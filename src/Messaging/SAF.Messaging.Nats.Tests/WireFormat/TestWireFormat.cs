// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using SAF.Messaging.Nats.WireFormat;

/// <summary>
/// The production wire format composition, as the plug-in builds it, and NATS messages as a subscription delivers them.
/// </summary>
internal static class TestWireFormat
{
    public static INatsMessageWriter Writer(bool enableBinaryPayloads = true)
        => ServiceCollectionExtensions.CreateWriter(new NatsConfiguration { EnableBinaryPayloads = enableBinaryPayloads });

    public static INatsMessageReader Reader(ILogger? logger = null)
        => ServiceCollectionExtensions.CreateReader(logger ?? NullLogger.Instance);

    public static ReadOnlyMemory<byte> Utf8Bytes(string? text) => text is null ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(text);

    public static NatsBody Utf8(string? text) => new(Utf8Bytes(text));

    /// <summary>
    /// A received message; like NATS, an empty body is no buffer at all.
    /// </summary>
    public static NatsMsg<NatsMemoryOwner<byte>> Msg(string subject, ReadOnlyMemory<byte> body, NatsHeaders? headers = null)
    {
        var data = default(NatsMemoryOwner<byte>);
        if (!body.IsEmpty)
        {
            data = NatsMemoryOwner<byte>.Allocate(body.Length);
            body.CopyTo(data.Memory);
        }

        return new NatsMsg<NatsMemoryOwner<byte>>(subject, null, body.Length, headers, data, null);
    }

    public static NatsMsg<NatsMemoryOwner<byte>> Msg(string subject, string? body, NatsHeaders? headers = null)
        => Msg(subject, Utf8Bytes(body), headers);
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;

/// <summary>
/// Turns messages into what a transport sends.
/// </summary>
/// <typeparam name="TWire">What the transport sends, for example a Redis value.</typeparam>
public interface IWireMessageWriter<TWire>
{
    /// <summary>The payload formats of the messages <see cref="TryWrite"/> writes rather than drops.</summary>
    MessageFormats Formats { get; }

    /// <param name="message">The message to send.</param>
    /// <param name="wire">What to send, if the result is <c>true</c>.</param>
    /// <param name="dropReason">Why the message is not sent, if the result is <c>false</c>.</param>
    bool TryWrite(Message message, out TWire wire, [NotNullWhen(false)] out string? dropReason);
}

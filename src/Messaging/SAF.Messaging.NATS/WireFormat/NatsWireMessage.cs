// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;

/// <summary>
/// What goes on the wire for one message: the body and the optional SAF headers.
/// </summary>
internal readonly record struct NatsWireMessage(string? Body, NatsHeaders? Headers);

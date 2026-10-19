// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;

namespace SAF.Messaging.Nats;

/// <summary>
/// Maps SAF routes to NATS subjects and back.
/// </summary>
/// <param name="Input">Translates topics and route filter patterns to NATS subjects.</param>
/// <param name="Output">Translates the subjects of received messages back to topics.</param>
internal sealed record NatsRouteTranslation(IInputRouteTranslator Input, IOutputRouteTranslator Output);

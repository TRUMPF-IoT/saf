// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// Well-known keys for built-in SAF messaging infrastructure plugins.
/// </summary>
public static class MessagingInfrastructureKeys
{
    public const string InProcess = "InProcess";
    public const string Redis = "Redis";
    public const string Cde = "Cde";
    public const string Nats = "Nats";
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Redis;

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = default!;
    public int Timeout { get; set; }

    /// <summary>
    /// Sends messages with a binary payload. Set it to <c>false</c> while nodes before SAF 11 use the same broker:
    /// they read such a message as corrupt text.
    /// </summary>
    public bool EnableBinaryPayloads { get; set; } = true;
}
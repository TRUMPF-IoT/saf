// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;
using SAF.Messaging.Contracts;

/// <summary>
/// Settings of a request sent with <see cref="IRequestClient.SendRequestAwaitFirstReply"/>.
/// </summary>
public sealed class RequestOptions
{
    /// <summary>
    /// Gets the payload formats the caller reads in the reply. <c>null</c> means text only.
    /// </summary>
    public MessageFormats? AcceptedReplyFormats { get; init; }

    /// <summary>
    /// Gets the prefix of the reply topic. <c>null</c> uses the default prefix.
    /// </summary>
    public string? ReplyTopicPrefix { get; init; }

    /// <summary>
    /// Gets the time to wait for the reply. <c>null</c> waits 5000 ms.
    /// </summary>
    public double? MillisecondsTimeoutTarget { get; init; }
}

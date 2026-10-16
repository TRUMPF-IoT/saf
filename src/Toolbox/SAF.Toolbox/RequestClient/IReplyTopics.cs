// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;

/// <summary>
/// The reply topics of one request client, and the subscriptions that receive replies on them.
/// </summary>
internal interface IReplyTopics : IDisposable
{
    /// <summary>
    /// Creates a unique reply topic under the prefix. Replies on it are received from the moment this returns.
    /// </summary>
    string Create(string prefix);
}

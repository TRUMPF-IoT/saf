// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;
using SAF.Messaging.Contracts;
using Serialization;

public interface IRequestClient
{
    void SetDefaultPrefix(string prefix);

    Task<TResponse?> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request, string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
        where TRequest : MessageRequestBase
        where TResponse : class;

    Task<TResponse?> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request, IJsonObjectConverter[] converters, string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
        where TRequest : MessageRequestBase
        where TResponse : class;

    Task<string?> SendRequestAwaitFirstAnswer<TRequest>(string topic, TRequest request,
        IJsonObjectConverter[] converters, string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
        where TRequest : MessageRequestBase;

    /// <summary>
    /// Sends a request message with the given payloads and returns the first reply as a whole message.
    /// The reply topic travels in <see cref="Message.ReplyTo"/> only, so the responder needs SAF 11.x.
    /// </summary>
    /// <returns>The reply, or <c>null</c> if none arrives in time.</returns>
    Task<Message?> SendRequestAwaitFirstReply(string topic, string? payload = null, byte[]? binaryPayload = null, RequestOptions? options = null);
}
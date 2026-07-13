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
}
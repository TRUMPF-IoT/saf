// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Messaging.Contracts;
using Heartbeat;
using Serialization;

internal sealed class RequestClient : IRequestClient, IDisposable
{
    private const double DefaultMillisecondsTimeoutTarget = 5000d;

    private readonly IMessagingInfrastructure _messaging;
    private readonly IHeartbeat _heartbeat;
    private readonly ILogger<RequestClient> _log;

    private readonly ConcurrentDictionary<string, OpenRequest> _openRequests = new();
        
    private string _defaultPrefix = "private/reply";
    private readonly string _postfix = $"requestclient/{Guid.NewGuid()}";
    private object? _subscriptionHandle;

    public RequestClient(IMessagingInfrastructure messaging, IHeartbeat heartbeat, ILogger<RequestClient>? log)
    {
        _messaging = messaging;
        _heartbeat = heartbeat;
        _log = log ?? NullLogger<RequestClient>.Instance;

        _heartbeat.Beat += CheckExpiringSubscriptions;

        _subscriptionHandle = _messaging.Subscribe($"*/{_postfix}", Handle);
    }

    public void SetDefaultPrefix(string prefix) => _defaultPrefix = prefix.TrimEnd('/');

    public Task<TResponse?> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request,
        string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
        where TRequest : MessageRequestBase
        where TResponse : class
    {
        return SendRequestAwaitFirstAnswer<TRequest, TResponse>(topic, request, null,
            replyTopicPrefix, millisecondsTimeoutTarget);
    }

    public async Task<TResponse?> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request,
        IJsonObjectConverter[]? converters, string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
        where TRequest : MessageRequestBase
        where TResponse : class
    {
        var resultJson = await SendRequestAwaitFirstAnswer(topic, request, converters, replyTopicPrefix,
            millisecondsTimeoutTarget);
        return resultJson != null
            ? JsonSerializer.Deserialize<TResponse>(resultJson, converters ?? [])
            : null;
    }

    public async Task<string?> SendRequestAwaitFirstAnswer<TRequest>(string topic, TRequest request, IJsonObjectConverter[]? converters,
        string? replyTopicPrefix = null, double? millisecondsTimeoutTarget = null) where TRequest : MessageRequestBase
    {
        var reply = await SendRequest(topic, replyTopicPrefix, millisecondsTimeoutTarget, replyTo =>
        {
            // SAF 10.x responders read the reply topic from the payload only.
            request.ReplyTo = replyTo;
            return new Message { Topic = topic, Payload = JsonSerializer.Serialize(request), ReplyTo = replyTo };
        });
        return reply?.Payload;
    }

    public Task<Message?> SendRequestAwaitFirstReply(string topic, string? payload = null, byte[]? binaryPayload = null, RequestOptions? options = null)
        => SendRequest(topic, options?.ReplyTopicPrefix, options?.MillisecondsTimeoutTarget, replyTo => new Message
        {
            Topic = topic,
            Payload = payload,
            BinaryPayload = binaryPayload,
            ReplyTo = replyTo,
            AcceptedReplyFormats = options?.AcceptedReplyFormats
        });

    private Task<Message?> SendRequest(string topic, string? replyTopicPrefix, double? millisecondsTimeoutTarget, Func<string, Message> createRequest)
    {
        if (string.IsNullOrEmpty(replyTopicPrefix))
            replyTopicPrefix = _defaultPrefix;

        var replyTo = string.Join("/", replyTopicPrefix.TrimEnd('/'), Guid.NewGuid().ToString(), _postfix);

        var heartbeatsUntilExpiry =
            (long)Math.Ceiling((millisecondsTimeoutTarget ?? DefaultMillisecondsTimeoutTarget) / _heartbeat.BeatCycleTimeMillis);

        try
        {
            var message = createRequest(replyTo);
            var currentBeat = _heartbeat.CurrentBeat;
            var openRequest = new OpenRequest(message, replyTo, currentBeat, currentBeat + heartbeatsUntilExpiry);

            if (!_openRequests.TryAdd(replyTo, openRequest))
            {
                _log.LogError("Failed to open request for {Topic}!", topic);
                return Task.FromResult<Message?>(null);
            }

            _messaging.Publish(message);
            return openRequest.Reply;
        }
        catch (Exception ex)
        {
            _openRequests.TryRemove(replyTo, out _);
            _log.LogError(ex, "Unexpected exception processing request {Topic}!", topic);
            return Task.FromResult<Message?>(null);
        }
    }

    public void Handle(Message message)
    {
        if (!_openRequests.TryRemove(message.Topic, out var request)) return;

        var format = message.GetFormat();
        if (!request.Message.AcceptsReplyFormat(format))
        {
            _log.LogWarning("The reply to request {Topic} carries payload format {Format}, which the request does not accept.",
                request.Topic, format);
        }

        request.Complete(message);
    }

    private void CheckExpiringSubscriptions(object? sender, HeartbeatEventArgs e)
    {
        var requestsToCancel = _openRequests.Values.Where(r => r.ExpiresOnHeartbeat <= e.CurrentBeat);

        foreach (var request in requestsToCancel.ToList())
        {
            if (!_openRequests.TryRemove(request.ReplyToTopic, out var removedRequest)) continue;

            var timeoutTimeMs = (e.CurrentBeat - request.PublishedOnHeartbeat) * _heartbeat.BeatCycleTimeMillis;
            _log.LogError("Cancelling timed out request '{Topic}' (replyTo={ReplyToTopic}) after {TimeoutMs}ms. Returning null.",
                request.Topic, removedRequest.ReplyToTopic, timeoutTimeMs);

            removedRequest.Complete(null);
        }
    }

    private sealed class OpenRequest(Message message, string replyToTopic, long publishedOnHeartbeat, long expiresOnHeartbeat)
    {
        private readonly TaskCompletionSource<Message?> _reply = new();

        public Message Message { get; } = message;
        public string Topic => Message.Topic;
        public string ReplyToTopic { get; } = replyToTopic;
        public long PublishedOnHeartbeat { get; } = publishedOnHeartbeat;
        public long ExpiresOnHeartbeat { get; } = expiresOnHeartbeat;
        public Task<Message?> Reply => _reply.Task;

        // Only the caller that removed the request from the open requests completes it.
        public void Complete(Message? reply) => _reply.SetResult(reply);
    }

    public void Dispose()
    {
        _log.LogInformation("Disposing RequestClient {RequestClientPostfix}.", _postfix);

        if (_subscriptionHandle != null)
        {
            _messaging.Unsubscribe(_subscriptionHandle);
            _subscriptionHandle = null;
        }

        var requests = _openRequests.Values.ToList();
        requests.ForEach(request =>
        {
            if (!_openRequests.TryRemove(request.ReplyToTopic, out _)) return;

            _log.LogError("Cancelling request '{Topic}' (replyTo={ReplyToTopic}) on shutdown. Returning null.",
                request.Topic, request.ReplyToTopic);
            request.Complete(null);
        });

        _openRequests.Clear();
    }
}



// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Common.Contracts;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.RequestClient
{
    internal class RequestClient : IRequestClient, IDisposable
    {
        private const double DefaultMillisecondsTimeoutTarget = 5000d;

        private readonly IMessagingInfrastructure _messaging;
        private readonly IHeartbeat _heartbeat;
        private readonly ILogger _log;

        private readonly ConcurrentDictionary<string, OpenRequest> _openRequests = new ConcurrentDictionary<string, OpenRequest>();
        
        private string _defaultPrefix = "private/reply";
        private readonly string _postfix = $"requestclient/{Guid.NewGuid()}";
        private object _subscriptionHandle;

        public RequestClient(IMessagingInfrastructure messaging, IHeartbeat heartbeat, ILogger<RequestClient> log)
        {
            _messaging = messaging;
            _heartbeat = heartbeat;
            _log = log as ILogger ?? NullLogger.Instance;

            _heartbeat.Beat += CheckExpiringSubscriptions;

            _subscriptionHandle = _messaging.Subscribe($"*/{_postfix}", Handle);
        }

        public void SetDefaultPrefix(string prefix) => _defaultPrefix = prefix.TrimEnd('/');

        public Task<TResponse> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request,
            string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
            where TRequest : MessageRequestBase
            where TResponse : class
        {
            return SendRequestAwaitFirstAnswer<TRequest, TResponse>(topic, request, null,
                replyTopicPrefix, millisecondsTimeoutTarget);
        }

        public async Task<TResponse> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request,
            IJsonObjectConverter[] converters, string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
            where TRequest : MessageRequestBase
            where TResponse : class
        {
            var resultJson = await SendRequestAwaitFirstAnswer(topic, request, converters, replyTopicPrefix,
                millisecondsTimeoutTarget);
            return resultJson != null
                ? JsonSerializer.Deserialize<TResponse>(resultJson, converters ?? new IJsonObjectConverter[] { })
                : null;
        }

        public Task<string> SendRequestAwaitFirstAnswer<TRequest>(string topic, TRequest request, IJsonObjectConverter[] converters,
            string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null) where TRequest : MessageRequestBase
        {
            if (string.IsNullOrEmpty(replyTopicPrefix))
                replyTopicPrefix = _defaultPrefix ?? string.Empty;
            
            var replyTo = string.Join("/", replyTopicPrefix.TrimEnd('/'), Guid.NewGuid().ToString(), _postfix);
            request.ReplyTo = replyTo;

            var heartbeatsUntilExpiry =
                (long)Math.Ceiling((millisecondsTimeoutTarget ?? DefaultMillisecondsTimeoutTarget) / _heartbeat.BeatCycleTimeMillis);

            var tcs = new TaskCompletionSource<string>();
            try
            {
                var currentBeat = _heartbeat.CurrentBeat;
                var openRequest = new OpenRequest
                {
                    Topic = topic,
                    ReplyToTopic = replyTo,
                    RequestHandlerAction = resultJson =>
                    {
                        if (!tcs.TrySetResult(resultJson))
                            _log.LogWarning($"SendRequestAwaitFirstAnswer: Couldn't set request result for {topic}");
                    },
                    TimeoutAction = () =>
                    {
                        if (!tcs.TrySetResult(null))
                            _log.LogWarning($"SendRequestAwaitFirstAnswer: Couldn't set empty result for {topic}");
                    },
                    CancelAction = () =>
                    {
                        if (!tcs.TrySetResult(null))
                            _log.LogWarning($"SendRequestAwaitFirstAnswer: Couldn't set empty result for {topic}");
                    },
                    PublishedOnHeartbeat = currentBeat,
                    ExpiresOnHeartbeat = currentBeat + heartbeatsUntilExpiry
                };

                if (_openRequests.TryAdd(replyTo, openRequest))
                {
                    _messaging.Publish(new Message
                    {
                        Topic = topic,
                        Payload = JsonSerializer.Serialize(request)
                    });
                }
                else
                {
                    _log.LogError($"SendRequestAwaitFirstAnswer: Failed to open request for {topic}!");
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"SendRequestAwaitFirstAnswer: Unexpected exception processing request {topic}!");
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }

        public void Handle(Message message)
        {
            if(!_openRequests.TryRemove(message.Topic, out var request)) return;

            request.RequestHandlerAction(message.Payload);
        }

        private void CheckExpiringSubscriptions(object sender, HeartbeatEventArgs e)
        {
            var requestsToCancel = _openRequests.Values.Where(r => r.ExpiresOnHeartbeat <= e.CurrentBeat);

            foreach (var request in requestsToCancel.ToList())
            {
                if (!_openRequests.TryRemove(request.ReplyToTopic, out var removedRequest)) continue;

                var timeoutTimeMs = (e.CurrentBeat - request.PublishedOnHeartbeat) * _heartbeat.BeatCycleTimeMillis;
                _log.LogError($"Cancelling timed out request '{request.Topic}' (replyTo={removedRequest.ReplyToTopic}) after {timeoutTimeMs}ms. Returning null.");

                removedRequest.TimeoutAction();
            }
        }

        private class OpenRequest
        {
            public long ExpiresOnHeartbeat { get; set; }
            public Action<string> RequestHandlerAction { get; set; }
            public Action TimeoutAction { get; set; }
            public Action CancelAction { get; set; }
            public string ReplyToTopic { get; set; }
            public string Topic { get; set; }
            public long PublishedOnHeartbeat { get; set; }
        }

        public void Dispose()
        {
            _log.LogInformation($"Disposing RequestClient {_postfix}.");

            if (_subscriptionHandle != null)
            {
                _messaging.Unsubscribe(_subscriptionHandle);
                _subscriptionHandle = null;
            }

            var requests = _openRequests.Values.ToList();
            requests.ForEach(request =>
            {
                if (!_openRequests.TryRemove(request.ReplyToTopic, out _)) return;

                _log.LogError($"Cancelling request '{request.Topic}' (replyTo={request.ReplyToTopic}) on shutdown. Returning null.");
                request.CancelAction();
            });

            _openRequests.Clear();
        }
    }
}
// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.Cde.Utils;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    /// <summary>
    /// Contains the data required for the registration of a subscriber.
    /// </summary>
    internal class RegistrySubscriptionRequest : SubscriptionRequest
    {
        // ReSharper disable once InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles
        public bool isRegistry { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }

    internal class RegistrySubscriptionResponse : RegistrySubscriptionRequest
    {
        // ReSharper disable once InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles
        public string instanceId { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }

    /// <summary>
    /// Manages the subscriptions of other nodes (each represented by <see cref="RemoteSubscriber"/>).
    /// These will be registered by a subscription event. These subscriptions are used by the parent
    /// <see cref="Publisher"/> to send Messages to all registered subscribers. This class also handles
    /// events for example discovery request or subsciber alive request.
    /// </summary>
    internal class SubscriptionRegistry : IDisposable
    {
        public const int AliveIntervalSeconds = 30;

        private readonly ReaderWriterLockSlim _syncSubscribers = new(LockRecursionPolicy.SupportsRecursion);
        private readonly IDictionary<string, IRemoteSubscriber> _subscribers = new Dictionary<string, IRemoteSubscriber>();

        private readonly Logger _log;
        private readonly ComLine _line;

        private Timer _subscriberLifetimeTimer;
        private int _checkingLifeTimes;

        private Timer _registryAliveTimer;
        private int _sendingAlive;
        private readonly string _instanceId = Guid.NewGuid().ToString("N");

        private readonly string _registryIdentity;

        public SubscriptionRegistry(ComLine line)
        {
            _log = new Logger(typeof(SubscriptionRegistry));
            _line = line;
            _registryIdentity = TheCommonUtils.SerializeObjectToJSONString(new RegistryIdentity(line.Address, _instanceId));
        }

        public async Task ConnectAsync(CancellationToken token)
        {
            _line.MessageReceived += HandleMessage;
            await _line.Subscribe(Engines.PubSub);

            _subscriberLifetimeTimer = new Timer(OnSubscriberLifetimeTimer, null,
                TimeSpan.FromSeconds(Subscriber.AliveIntervalSeconds * 2),
                TimeSpan.FromSeconds(Subscriber.AliveIntervalSeconds * 2));

            _registryAliveTimer = new Timer(OnRegistryAliveTimer, null,
                TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(AliveIntervalSeconds));
        }

        public void Broadcast(Topic topic, Message message, string userId, RoutingOptions routingOptions)
        {
            _syncSubscribers.EnterReadLock();
            try
            {
                foreach (var subscriber in _subscribers.Values)
                {
                    if(!subscriber.IsRoutingAllowed(routingOptions)) continue;
                    if (!subscriber.IsMatch(topic.Channel)) continue;

                    TSM tsm;
                    var messageTxt = $"{MessageToken.Publish}:{new Topic(topic.Channel, topic.MsgId, subscriber.Version).ToTsmTxt()}";
                    if (subscriber.Version == PubSubVersion.V1)
                    {
                        tsm = new TSM(subscriber.TargetEngine, messageTxt, message.Payload)
                        {
                            UID = userId
                        };
                    }
                    else
                    {
                        tsm = new TSM(subscriber.TargetEngine, messageTxt, TheCommonUtils.SerializeObjectToJSONString(message))
                        {
                            UID = userId
                        };
                    }

                    _log.LogDebug($"Send {MessageToken.Publish} ({topic.Channel}), origin: {_line.Address}, target: {subscriber.Tsm.ORG}");
                    _line.AnswerToSender(subscriber.Tsm, tsm);
                }
            }
            finally
            {
                _syncSubscribers.ExitReadLock();
            }
        }

        /// <summary>
        /// Register a RemoteSubscriber under his ID.
        /// </summary>
        private void HandleSubscribe(TSM message)
        {
            var request = TheCommonUtils.DeserializeJSONStringToObject<RegistrySubscriptionRequest>(message.PLS);

            const string all = "*";
            var topics = request.topics ?? new string[0];

            var newPatterns = topics.Distinct().ToList();
            if (newPatterns.Count == 0)
            {
                // Allow subscription without parameter which subscribes to all topics.
                newPatterns.Add(all);
            }

            _syncSubscribers.EnterWriteLock();
            try
            {
                if (!_subscribers.TryGetValue(message.ORG, out var subscriber))
                {
                    subscriber = new RemoteSubscriber(message, newPatterns, request);
                    _subscribers.Add(message.ORG, subscriber);
                    _log.LogDebug($"HandleSubscribe: new {message.ORG}, topics {String.Join(",", topics)}");
                }
                else
                {
                    subscriber.Touch();
                    subscriber.AddPatterns(newPatterns);
                    _log.LogDebug($"HandleSubscribe: touch {message.ORG}, topics {String.Join(",", topics)}");
                }

                var response = new RegistrySubscriptionResponse
                {
                    id = request.id,
                    instanceId = _instanceId,
                    isRegistry = request.isRegistry,
                    topics = request.topics,
                    version = PubSubVersion.Latest
                };
                var tsm = new TSM(subscriber.TargetEngine, MessageToken.SubscribeResponse, TheCommonUtils.SerializeObjectToJSONString(response));
                _log.LogDebug($"Send {MessageToken.SubscribeResponse}, origin: {_line.Address}, target: {subscriber.Tsm.ORG}");
                _line.AnswerToSender(subscriber.Tsm, tsm);
            }
            finally
            {
                _syncSubscribers.ExitWriteLock();
            }
        }

        private void HandleUnsubscribe(TSM message)
        {
            _syncSubscribers.EnterWriteLock();
            try
            {
                var request = TheCommonUtils.DeserializeJSONStringToObject<SubscriptionRequest>(message.PLS);

                var topics = request.topics;
                if (topics == null || topics.Length == 0)
                {
                    _subscribers.Remove(message.ORG); // Allow complete cancellation of subscriptions.        
                    return;
                }

                if (!_subscribers.TryGetValue(message.ORG, out var subscriber)) return;

                subscriber.Touch();
                subscriber.RemovePatterns(topics);
                              
                if (!subscriber.HasPatterns)
                {
                    _subscribers.Remove(message.ORG);
                }
            }
            finally
            {
                _syncSubscribers.ExitWriteLock();
            }
        }

        private void HandleMessage(ICDEThing sender, object msg)
        {
            if (msg is not TheProcessMessage message) return;

            if (message.Message.TXT.StartsWith(MessageToken.Publish))
                HandlePublication(message);
            else if (message.Message.TXT.StartsWith(MessageToken.DiscoveryRequest))
                HandleDiscoveryRequest(message);
            else if (message.Message.TXT.StartsWith(MessageToken.SubscribeRequest))
                HandleSubscribe(message.Message);
            else if (message.Message.TXT.StartsWith(MessageToken.Unsubscribe))
                HandleUnsubscribe(message.Message);
            else if (message.Message.TXT.StartsWith(MessageToken.SubscriberAlive))
                HandleSubscriberAlive(message.Message);
            else if (message.Message.TXT.StartsWith(MessageToken.SubscriberShutdown))
                HandleSubscriberShutdown(message.Message);
        }

        private void HandlePublication(TheProcessMessage message)
        {
            var topicTxt = message.Message.TXT.Remove(0, $"{MessageToken.Publish}:".Length);
            if (message.Message.ENG == Engines.RemotePubSub)
            {
                // dispatch message from remote subscriber to mesh
                DispatchPublication(topicTxt, message.Message);
            }
        }

        private void DispatchPublication(string topicTxt, TSM message)
        {
            var topic = topicTxt.ToTopic();
            if (topic == null)
            {
                _log.LogWarning($"Detected invalid cde-pubsub topic format: '{topicTxt}' (ENG={message.ENG}) -> ignoring message");
                return;
            }

            var msg = topic.Version == PubSubVersion.V1
                ? new Message {Topic = topic.Channel, Payload = message.PLS}
                : TheCommonUtils.DeserializeJSONStringToObject<Message>(message.PLS);

            Broadcast(topic, msg, message.UID, RoutingOptions.All);
        }

        private void HandleDiscoveryRequest(TheProcessMessage message)
        {
            _log.LogDebug($"HandleDiscoveryRequest {message.Message.ORG}");
            var reply = new TSM(message.Message.ENG, MessageToken.DiscoveryResponse, _registryIdentity);
            _log.LogDebug($"Send {MessageToken.DiscoveryResponse}, origin: {_line.Address}, target: {message.Message.ORG}");
            _line.AnswerToSender(message.Message, reply);
        }

        private void HandleSubscriberAlive(TSM message)
        {
            _syncSubscribers.EnterUpgradeableReadLock();
            try
            {
                if (!_subscribers.TryGetValue(message.ORG, out var known))
                {
                    // in case that we receive a subscriber alive telegram for an unknown subscriber
                    // we force the remote part to resend a subscribe request
                    var tsm = new TSM(message.ENG, MessageToken.SubscribeTrigger, _registryIdentity);
                    _line.AnswerToSender(message, tsm);
                    _log.LogWarning($"Unknown subscriber: triggered resubscribe for origin {message.ORG}");
                    return;
                }
                _log.LogDebug($"Touch known subscriber origin: {message.ORG}");

                _syncSubscribers.EnterWriteLock();
                try
                {
                    known.Touch();
                }
                finally
                {
                    _syncSubscribers.ExitWriteLock();
                }
            }
            finally
            {
                _syncSubscribers.ExitUpgradeableReadLock();
            }
        }

        private void HandleSubscriberShutdown(TSM message)
        {
            _syncSubscribers.EnterWriteLock();
            try
            {
                _subscribers.Remove(message.ORG);
            }
            finally
            {
                _syncSubscribers.ExitWriteLock();
            }
        }

        private void OnSubscriberLifetimeTimer(object state)
        {
            if (Interlocked.Exchange(ref _checkingLifeTimes, 1) == 1) return;

            _syncSubscribers.EnterWriteLock();
            try
            {
                // remove stale subscribers (no alive telegram since last run)
                foreach (var subscriber in _subscribers.Values.Where(s => !s.IsAlive).ToArray())
                {
                    _subscribers.Remove(subscriber.Tsm.ORG);

                    _log.LogWarning($"Subscriber died: sending timeout error to {subscriber.Tsm.ORG}");
                    var timeout = new TSM(subscriber.TargetEngine, MessageToken.Error, "timeout");
                    _line.AnswerToSender(subscriber.Tsm, timeout);
                }
            }
            finally
            {
                _syncSubscribers.ExitWriteLock();
                Interlocked.Exchange(ref _checkingLifeTimes, 0);
            }
        }

        private void OnRegistryAliveTimer(object state)
        {
            if (Interlocked.Exchange(ref _sendingAlive, 1) == 1) return;

            try
            {
                // send to other registries in the mesh
                var tsm = new TSM(Engines.PubSub, MessageToken.RegistryAlive, _registryIdentity);
                tsm.SetToServiceOnly(true);
                _log.LogDebug($"Broadcast {MessageToken.RegistryAlive}, origin: {_line.Address}");
                _line.Broadcast(tsm);
            }
            finally
            {
                Interlocked.Exchange(ref _sendingAlive, 0);
            }
        }

        public void Dispose()
        {
            _registryAliveTimer?.Dispose();
            _subscriberLifetimeTimer?.Dispose();

            // send to other registries in the mesh
            var tsm = new TSM(Engines.PubSub, MessageToken.RegistryShutdown, _registryIdentity);
            tsm.SetToServiceOnly(true);
            _log.LogDebug($"Broadcast {MessageToken.RegistryShutdown}, origin: {_line.Address}");
            _line.Broadcast(tsm);

            // send to browser nodes
            tsm = new TSM(Engines.RemotePubSub, MessageToken.RegistryShutdown, _registryIdentity);
            tsm.SetToNodesOnly(true);
            _line.Broadcast(tsm);
        }
    }
}

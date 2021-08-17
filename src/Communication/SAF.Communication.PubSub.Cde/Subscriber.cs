// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using SAF.Communication.Cde;
using SAF.Communication.Cde.Utils;
using SAF.Communication.PubSub.Cde.MessageHandler;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    /// <summary>
    /// Manages all subscribers (which are implemented as <see cref="SubscriptionInternal"/>) 
    /// and the messages from the C-DEngine to them. Coordinates organizational event-driven
    /// and scheduled calls to the C-DEngine via <see cref="ComLine"/>. The referenced
    /// <see cref="RemoteRegistryLifetimeHandler"/> is used to manage the lifetime of the other
    /// subscribers in the mesh.<br/>
    /// Messages from SAF toward C-DEngine runs via <see cref="Publisher"/>.
    /// </summary>
    public class Subscriber : ISubscriber, IDisposable
    {
        internal const int AliveIntervalSeconds = 30;
        private const int DiscoveryResponseTimeoutMs = 3000;
        private const int SubscribeResponseTimeoutMs = 3000;

        private readonly Logger _log;
        private readonly ComLine _line;
        private readonly CancellationTokenSource _tokenSource;
        private bool _disposed;

        private readonly Dictionary<string, CountdownEvent> _subscriptions = new(); // temporarly used to broadcast a subscribe request to all known registered nodes
        private readonly ConcurrentDictionary<Guid, ISubscription> _subscribers = new();
        private readonly ManualResetEventSlim _registryDiscoveredEvent = new(false);

        private Timer _aliveTimer;
        private int _sendingAlive;

        private RemoteRegistryLifetimeHandler _registryLifetimeHandler;
        private MessageListener _messageListener;
        private string _registryIdentity;

        public event Action<string, string, TheProcessMessage> MessageEvent;

        public Subscriber(ComLine line, Publisher publisher)
            : this(line, publisher, new CancellationTokenSource())
        { }

        public Subscriber(ComLine line, Publisher publisher, CancellationToken token)
            : this(line, publisher, CancellationTokenSource.CreateLinkedTokenSource(token))
        { }

        public Subscriber(ComLine line, Publisher publisher, CancellationTokenSource tokenSource)
        {
            _log = new Logger(typeof(Subscriber));
            _line = line;
            _tokenSource = tokenSource;

            InitAsync(publisher).Wait(_tokenSource.Token);
        }

        public ISubscription Subscribe(params string[] patterns)
            => Subscribe(RoutingOptions.All, patterns);

        public ISubscription Subscribe(RoutingOptions routingOptions, params string[] patterns)
        {
            if (patterns == null || !patterns.Any()) patterns = new[] { "*" };

            var subscription = new SubscriptionInternal(this, routingOptions, patterns);
            RemoteSubscribe(subscription.Id, routingOptions, patterns);

            _subscribers.TryAdd(subscription.Id, subscription);
            return subscription;
        }

        public void Unsubscribe(ISubscription subscription)
        {
            if (_subscribers.TryRemove(subscription.Id, out var _))
            {
                RemoteUnsubscribe(subscription);
            }
        }

        private async Task InitAsync(Publisher publisher)
        {
            _line.MessageReceived += HandleMessage;
            await _line.Subscribe(Engines.PubSub);

            _aliveTimer = new Timer(OnAliveTimer, null,
                TimeSpan.FromSeconds(AliveIntervalSeconds),
                TimeSpan.FromSeconds(AliveIntervalSeconds));

            _registryLifetimeHandler = new RemoteRegistryLifetimeHandler();
            _registryLifetimeHandler.RegistryUp += OnRegistryUp;

            BroadcastDiscoveryRequestAndAwaitFirstResponse();

            _messageListener = new MessageListener(this, publisher);
        }

        private void BroadcastDiscoveryRequest()
        {
            var tsm = new TSM(Engines.PubSub, MessageToken.DiscoveryRequest, Engines.PubSub);
            tsm.SetToServiceOnly(true);
            _log.LogDebug($"Broadcast {MessageToken.DiscoveryRequest}, origin: {_line.Address}");
            _line.Broadcast(tsm);
        }

        /// <summary>
        /// Send a discovery request to all nodes in the mesh. The answer will be processed by 
        /// the <see cref="RemoteRegistryLifetimeHandler"/>. As a side effect the list of the
        /// known nodes will be filled.
        /// </summary>
        private void BroadcastDiscoveryRequestAndAwaitFirstResponse()
        {
            const int loopTimeoutMs = 500;
            var maxLoops = Convert.ToInt32(Math.Max(1.0, Math.Ceiling(Convert.ToDouble(DiscoveryResponseTimeoutMs / loopTimeoutMs))));
            var loop = 0;
            while (!_registryDiscoveredEvent.IsSet && loop++ < maxLoops)
            {
                BroadcastDiscoveryRequest();
                _registryDiscoveredEvent.Wait(loopTimeoutMs, _tokenSource.Token);
            }
        }

        /// <summary>
        /// If the subscriber is no longer current in a registered node, then he must subscribe again.
        /// </summary>
        private void OnRegistryUp(TSM registry, string reasonToken)
        {
            _log.LogInformation($"Registry {registry.ORG} updated, reason = {reasonToken}");
            _registryDiscoveredEvent.Set();
            if (reasonToken.StartsWith(MessageToken.SubscribeResponse)) return;

            RemoteResubscribe(registry);
        }

        private void RemoteResubscribe(TSM registry)
        {
            var subscribedTopics = CollectSubscribedTopics(registry).ToArray();
            if (subscribedTopics.Length == 0) return;

            _log.LogInformation($"Resubscribe at {registry.ORG}");
            SendSubscribeRequest(registry, subscribedTopics);
        }

        private void RemoteSubscribe(Guid subscriptionId, RoutingOptions routingOptions, string[] topics)
        {
            var subscribedTopics = CollectSubscribedTopics(routingOptions).ToArray();
            var subscribeTopics = topics.Where(t => !subscribedTopics.Contains(t));

            BroadcastSubscribeRequestAndAwaitResponses(subscriptionId, routingOptions, subscribeTopics.ToArray());
        }

        private IEnumerable<string> CollectSubscribedTopics(RoutingOptions routingOptions)
        {
            var relevantSubscriptions = _subscribers.Values.Where(s => routingOptions == RoutingOptions.All || s.RoutingOptions == RoutingOptions.All || s.RoutingOptions == routingOptions);
            return relevantSubscriptions.SelectMany(s => s.Patterns).Distinct();
        }

        private IEnumerable<string> CollectSubscribedTopics(TSM registryTsm)
        {
            var registryRouting = registryTsm.IsLocalHost() ? RoutingOptions.Local : RoutingOptions.Remote;
            var relevantSubscriptions = _subscribers.Values.Where(s => s.RoutingOptions == RoutingOptions.All || s.RoutingOptions == registryRouting);
            return relevantSubscriptions.SelectMany(s => s.Patterns).Distinct();
        }

        private void BroadcastSubscribeRequestAndAwaitResponses(Guid subscriptionId, RoutingOptions routingOptions, string[] topics)
        {
            if (topics.Length <= 0) return;

            var registries = _registryLifetimeHandler.Registries.Where(reg => reg.IsRoutingAllowed(routingOptions)).ToList();
            if (registries.Count == 0) return;

            var request = CreateSubscriptionRequest(subscriptionId, topics);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
            var requestEvent = new CountdownEvent(registries.Count);
            try
            {
                lock (_subscriptions) _subscriptions[request.id] = requestEvent;
                registries.ForEach(reg => SendSubscribeRequest(reg, request));
                requestEvent.Wait(SubscribeResponseTimeoutMs, linkedCts.Token);
            }
            finally
            {
                lock (_subscriptions) _subscriptions.Remove(request.id);
                requestEvent.Dispose();
                linkedCts.Dispose();
            }
        }

        private RegistrySubscriptionRequest CreateSubscriptionRequest(string[] topics)
            => CreateSubscriptionRequest(Guid.NewGuid(), topics);

        private RegistrySubscriptionRequest CreateSubscriptionRequest(Guid subId, string[] topics)
        {
            return new()
            {
                id = subId.ToString("N"),
                topics = topics,
                isRegistry = true,
                version = PubSubVersion.Latest
            };
        }

        private void RemoteUnsubscribe(ISubscription sub)
        {
            var subscribedTopics = CollectSubscribedTopics(sub.RoutingOptions);
            var unsubscribeTopics = sub.Patterns.Where(p => !subscribedTopics.Contains(p));
            
            BroadcastUnsubscribeRequest(sub.RoutingOptions, unsubscribeTopics.ToArray());
        }

        private void BroadcastUnsubscribeRequest(RoutingOptions routingOptions, string[] topics)
        {
            if (topics.Length <= 0) return;

            var registries = _registryLifetimeHandler.Registries.Where(reg => reg.IsRoutingAllowed(routingOptions)).ToList();
            if (registries.Count == 0) return;

            var request = CreateSubscriptionRequest(topics);
            registries.ForEach(reg =>
            {
                var tsm = new TSM(Engines.PubSub, MessageToken.Unsubscribe, TheCommonUtils.SerializeObjectToJSONString(request));
                tsm.SetToServiceOnly(true);
                _log.LogDebug($"Send {MessageToken.Unsubscribe}, origin: {_line.Address}, target: {reg.ORG}");
                _line.AnswerToSender(reg, tsm);
            });
        }

        private void OnMessageEvent(string topic, string msgVersion, TheProcessMessage msg)
            => MessageEvent?.Invoke(topic, msgVersion, msg);

        private void HandleMessage(ICDEThing sender, object pMsg)
        {
            if (pMsg is not TheProcessMessage msg) return;
            if (msg.Message.ENG != Engines.PubSub) return; //  accept only non remote-subscriber publications

            _log.LogDebug($"Recived message: {msg.Message.TXT}, origin: {msg.Message.ORG}, payload: {msg.Message.PLS}");
            if (msg.Message.TXT.StartsWith(MessageToken.Publish))
            {
                HandlePublication(msg);
            }
            else if (msg.Message.TXT.StartsWith(MessageToken.DiscoveryResponse))
            {
                HandleDiscoveryResponse(msg);
            }
            else if (msg.Message.TXT.StartsWith(MessageToken.SubscribeResponse))
            {
                HandleSubscriptionResponse(msg);
            }
            else if (msg.Message.TXT.StartsWith(MessageToken.SubscribeTrigger))
            {
                HandleSubscribeTrigger(msg);
            }
            else if (msg.Message.TXT.StartsWith(MessageToken.Error))
            {
                HandleError(msg);
            }
            _registryLifetimeHandler?.HandleMessage(msg);
            _log.LogDebug($"Finished message: {msg.Message.TXT}, origin: {msg.Message.ORG}, payload: {msg.Message.PLS}");
        }

        private void SendSubscribeRequest(TSM registryTsm, string[] topics)
            => SendSubscribeRequest(registryTsm, CreateSubscriptionRequest(topics));

        private void SendSubscribeRequest(TSM registryTsm, RegistrySubscriptionRequest request)
        {
            var tsm = new TSM(Engines.PubSub, MessageToken.SubscribeRequest, TheCommonUtils.SerializeObjectToJSONString(request));
            tsm.SetToServiceOnly(true);
            _log.LogDebug($"Send {MessageToken.SubscribeRequest}, origin: {_line.Address}, target: {registryTsm.ORG}, topics {string.Join(",", request.topics)}");
            _line.AnswerToSender(registryTsm, tsm);
        }

        private void HandleDiscoveryResponse(TheProcessMessage msg)
        {
            lock (_subscriptions)
            {
                if (_registryIdentity == null && msg.Message.ORG.Equals(_line.Address))
                {
                    _registryIdentity = msg.Message.PLS;
                    _log.LogDebug($"_registryIdentity set to: {_registryIdentity}");
                }
            }
        }

        private void HandleSubscriptionResponse(TheProcessMessage msg)
        {
            var response = TheCommonUtils.DeserializeJSONStringToObject<RegistrySubscriptionRequest>(msg.Message.PLS);
            lock(_subscriptions)
            {
                if (!_subscriptions.TryGetValue(response.id, out var requestEvent)) return;

                if(requestEvent.Signal())
                {
                    _subscriptions.Remove(response.id);
                }
            }
        }

        private void HandleSubscribeTrigger(TheProcessMessage msg)
        {
            _log.LogInformation("Subscribe triggered");
            RemoteResubscribe(msg.Message);
        }

        private void HandlePublication(TheProcessMessage msg)
        {
            var topicTxt = msg.Message.TXT.Remove(0, $"{MessageToken.Publish}:".Length);
            var msgTopic = topicTxt.ToTopic();
            if (msgTopic == null) return;

            OnMessageEvent(msgTopic.Channel, msgTopic.Version, msg);
        }

        private void HandleError(TheProcessMessage msg)
        {
            _log.LogWarning("Subscription timed out");
            RemoteResubscribe(msg.Message);
        }

        private void OnAliveTimer(object state)
        {
            if (Interlocked.Exchange(ref _sendingAlive, 1) == 1) return;

            try
            {
                var tsm = new TSM(Engines.PubSub, MessageToken.SubscriberAlive, _registryIdentity);
                tsm.SetToServiceOnly(true);
                _log.LogDebug($"broadcast {MessageToken.SubscriberAlive}, origin: {_line.Address}, Ident: {_registryIdentity}");
                _line.Broadcast(tsm);
            }
            finally
            {
                Interlocked.Exchange(ref _sendingAlive, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _messageListener?.Dispose();
            
            _registryLifetimeHandler?.Dispose();
            _aliveTimer?.Dispose();

            _registryDiscoveredEvent?.Dispose();

            var tsm = new TSM(Engines.PubSub, MessageToken.SubscriberShutdown);
            tsm.SetToServiceOnly(true);
            _log.LogDebug($"Broadcast {MessageToken.SubscriberShutdown}, origin: {_line.Address}");
            _line.Broadcast(tsm);

            _tokenSource.Cancel();
            _tokenSource.Dispose();
        }
    }
}
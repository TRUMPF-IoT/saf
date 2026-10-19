// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;
using SAF.Communication.PubSub.Cde.WireFormat;

namespace SAF.Communication.PubSub.Cde;

/// <summary>
/// Publishes messages (using <see cref="SubscriptionRegistry"/>) to all registered subscribers via
/// <see cref="ComLine"/>.<br/>
/// Messages from C-DEngine toward SAF runs via <see cref="Subscriber"/>.
/// </summary>
public class Publisher : IPublisher, IDisposable
{
    private bool _disposed;
    private readonly ComLine _line; //Only needed temporarily to initialize the SubscriptionRegistry object.
    private readonly CancellationTokenSource _tokenSource;
    private readonly ITsmMessageEncoder _encoder;
    private readonly ITsmMessageDecoder _decoder;
    internal ISubscriptionRegistry? _subscriptionRegistry;

    public Publisher(ComLine line)
        : this(line, new CancellationTokenSource())
    { }

    public Publisher(ComLine line, CancellationToken token)
        : this(line, CancellationTokenSource.CreateLinkedTokenSource(token))
    { }

    public Publisher(ComLine line, CancellationTokenSource tokenSource)
        : this(line, tokenSource, TsmWireFormats.CreateCodec())
    { }

    private Publisher(ComLine line, CancellationTokenSource tokenSource, TsmMessageCodec codec)
        : this(line, tokenSource, codec, codec)
    { }

    internal Publisher(ComLine line, CancellationTokenSource tokenSource, ITsmMessageEncoder encoder, ITsmMessageDecoder decoder)
    {
        _line = line;
        _tokenSource = tokenSource;
        _encoder = encoder;
        _decoder = decoder;
    }

    public async Task<IPublisher> ConnectAsync()
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
        try
        {
            _subscriptionRegistry = new SubscriptionRegistry(_line, _encoder, _decoder);
            await _subscriptionRegistry.ConnectAsync(linkedCts.Token);
            return this;
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    public void Publish(string channel, string message)
    {
        Publish(new Message { Topic = channel, Payload = message});
    }

    public void Publish(string channel, string message, RoutingOptions routingOptions)
    {
        Publish(new Message { Topic = channel, Payload = message }, Guid.Empty, routingOptions);
    }

    public void Publish(Message message)
    {
        Publish(message, Guid.Empty);
    }

    public void Publish(Message message, RoutingOptions routingOptions)
    {
        Publish(message, Guid.Empty, routingOptions);
    }

    public void Publish(Message message, Guid userId)
    {
        Publish(message, $"{userId}");
    }

    public void Publish(Message message, Guid userId, RoutingOptions routingOptions)
    {
        Publish(message, $"{userId}", routingOptions);
    }

    public void Publish(Message message, string userId)
    {
        Publish(message, userId, RoutingOptions.All);
    }

    public void Publish(Message message, string userId, RoutingOptions routingOptions)
    {
        var t = new Topic { Channel = message.Topic, MsgId = Guid.NewGuid().ToString("N") };
        _subscriptionRegistry?.Broadcast(t, message, userId, routingOptions);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        if (_disposed) return;
        _disposed = true;

        _tokenSource.Cancel();
        _tokenSource.Dispose();

        _subscriptionRegistry?.Dispose();
    }
}


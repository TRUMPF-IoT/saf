// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
///     Provides access to the publish / subscribe infrastructure of the underlying networking technology.
/// </summary>
public interface IMessagingInfrastructure
{
    /// <summary>
    ///     Publishes a pub / sub message to the mesh.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    void Publish(Message message);

    /// <summary>
    ///     Gets the payload formats <see cref="Publish"/> sends on the topic. Without <see cref="MessageFormats.Binary"/>,
    ///     a message with a <see cref="Message.BinaryPayload"/> does not arrive, for example while sending binary payloads
    ///     is disabled.
    /// </summary>
    /// <remarks>
    ///     Says nothing about single subscribers: C-DEngine still skips subscribers on SAF 10.x or older for a binary payload.
    ///     The default is text only, so callers do not hand binary payloads to infrastructures written without them.
    /// </remarks>
    /// <param name="topic">The topic to publish on.</param>
    MessageFormats GetPublishableFormats(string topic) => MessageFormats.Text;

    /// <summary>
    ///     Subscribes to all received routes in the mesh.
    /// </summary>
    /// <typeparam name="TMessageHandler">The type of the message handler.</typeparam>
    /// <returns>A subscription id to provide unsubscribe functionality.</returns>
    object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler;

    /// <summary>
    ///     Subscribes to the specified route filter in the mesh.
    /// </summary>
    /// <typeparam name="TMessageHandler">The type of the message handler.</typeparam>
    /// <param name="routeFilterPattern">The route filter pattern (RegEx).</param>
    /// <returns>A subscription id to provide unsubscribe functionality.</returns>
    object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler;

    /// <summary>
    ///     Subscribes to all received routes in the mesh.
    /// </summary>
    /// <param name="handler">A lambda which receives an instance of type {Message}.</param>
    /// <returns>A subscription id to provide unsubscribe functionality.</returns>
    object Subscribe(Action<Message> handler);

    /// <summary>
    ///     Subscribe to the specified route filter with the given lambda expression.
    /// </summary>
    /// <param name="routeFilterPattern">The route filter pattern (RegEx).</param>
    /// <param name="handler">A lambda which receives an instance of type {Message}.</param>
    /// <returns>A subscription id to provide unsubscribe functionality.</returns>
    object Subscribe(string routeFilterPattern, Action<Message> handler);

    /// <summary>
    ///     Unsubscribes the specified subscription.
    /// </summary>
    /// <param name="subscription">The subscription identifier object (returned by subscribe).</param>
    void Unsubscribe(object subscription);
}


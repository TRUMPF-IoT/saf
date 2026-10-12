// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts.Tests;

using Xunit;

public class MessagingInfrastructureTests
{
    [Theory]
    [InlineData("p", false)]
    [InlineData(null, true)]
    public void CanPublish_IsTrueByDefault(string? payload, bool hasBinaryPayload)
    {
        IMessagingInfrastructure messaging = new MessagingWithoutCanPublish();

        Assert.True(messaging.CanPublish(new Message { Topic = "t", Payload = payload, BinaryPayload = hasBinaryPayload ? [1] : null }));
    }

    private sealed class MessagingWithoutCanPublish : IMessagingInfrastructure
    {
        public void Publish(Message message) { }
        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => new();
        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler => new();
        public object Subscribe(Action<Message> handler) => new();
        public object Subscribe(string routeFilterPattern, Action<Message> handler) => new();
        public void Unsubscribe(object subscription) { }
    }
}

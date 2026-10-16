// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts.Tests;

using Xunit;

public class MessagingInfrastructureTests
{
    [Fact]
    public void GetPublishableFormats_IsTextOnlyByDefault()
    {
        IMessagingInfrastructure messaging = new MessagingWithoutPublishableFormats();

        Assert.Equal(MessageFormats.Text, messaging.GetPublishableFormats("t"));
    }

    private sealed class MessagingWithoutPublishableFormats : IMessagingInfrastructure
    {
        public void Publish(Message message) { }
        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => new();
        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler => new();
        public object Subscribe(Action<Message> handler) => new();
        public object Subscribe(string routeFilterPattern, Action<Message> handler) => new();
        public void Unsubscribe(object subscription) { }
    }
}

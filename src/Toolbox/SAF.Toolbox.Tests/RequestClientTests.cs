// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using NSubstitute;
using SAF.Common;
using SAF.Common.Contracts;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.Serialization;
using Xunit;

namespace SAF.Toolbox.Tests
{
    public class RequestClientTests
    {
        [Fact]
        public async Task ReturnsValueOfResponseTimeout()
        {
            // Arrange
            string? replyToTopic = null;
            var messagingMock = Substitute.For<IMessagingInfrastructure>();
            messagingMock.When(m => m.Publish(Arg.Any<Message>()))
                .Do(ci =>
                {   // "catch" reply to of sent message
                    var msg = JsonSerializer.Deserialize<DummyRequest>(ci.Arg<Message>().Payload!)!;
                    replyToTopic = msg.ReplyTo;
                });

            var heartbeatMock = Substitute.For<IHeartbeat>();
            heartbeatMock.BeatCycleTimeMillis.Returns(1000);

            // Act
            var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
            var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest());
            sut.Handle(new Message { Topic = replyToTopic!, Payload = "{ \"myDummyValue\": 123 }" });

            // Assert
            var response = await task;
            Assert.True(task.IsCompleted);
            Assert.NotNull(response);
            Assert.Equal(123, response.MyDummyValue);
        }

        [Fact]
        public async Task ReturnsNullOnTimeout()
        {
            // Arrange
            var messagingMock = Substitute.For<IMessagingInfrastructure>();
            var heartbeatMock = Substitute.For<IHeartbeat>();
            heartbeatMock.BeatCycleTimeMillis.Returns(1000);

            // Act
            var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
            var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest(), millisecondsTimeoutTarget: 5000);

            heartbeatMock.Beat += Raise.EventWith(this, new HeartbeatEventArgs(1000, 5));
            var response = await task;

            // Assert
            Assert.True(task.IsCompleted);
            Assert.Null(response); // timed out!
        }

        [Fact]
        public async Task ReturnsNullOnDisposal()
        {
            // Arrange
            var messagingMock = Substitute.For<IMessagingInfrastructure>();
            var heartbeatMock = Substitute.For<IHeartbeat>();
            heartbeatMock.BeatCycleTimeMillis.Returns(1000);

            // Act
            var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
            var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest(), millisecondsTimeoutTarget: 30000);

            await Task.Delay(10);

            sut.Dispose();
            var response = await task;

            // Assert
            Assert.True(task.IsCompleted);
            Assert.Null(response); // canceled!
        }

        private class DummyRequest : MessageRequestBase
        {
        }

        private class DummyResponse
        {
            public int MyDummyValue { get; set; }
        }
    }
}

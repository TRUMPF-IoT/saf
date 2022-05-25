using NSubstitute;
using SAF.Common;
using SAF.DevToolbox.TestRunner;

namespace SAF.DevToolbox.Tests.TestRunner;

public class TestSequenceBaseTests
{
    private class TestSequenceBaseTestInstance : TestSequenceBase
    {
        public TestSequenceBaseTestInstance(IMessagingInfrastructure messaging) : base(messaging)
        {
        }

        public override void Run()
        {
        }

        public IDisposable SubscribeAction(string topic, Action<string> action)
            => PayloadToVariable(topic, action);

        public IDisposable SubscribeAction<T>(string topic, Action<string> action) where T: IMessageHandler
            => PayloadToVariable<T>(topic, action);

        public void WaitForPayload(ref string? payload, int timeoutSeconds)
            => WaitForValueSet(ref payload, timeoutSeconds);
    }

    [Fact]
    public void CanHandleReturnsTrue()
    {
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        
        var instance = new TestSequenceBaseTestInstance(messagingMock);
        Assert.True(instance.CanHandle(new Message()));
    }

    [Fact]
    public void PayloadToVariableActionTriggered()
    {
        const string topic = "testTopic";
        const string payload = "testPayload";

        var subscribeId = "1234";
        Action<Message>? subscribedAction = null;
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        messagingMock.Subscribe(Arg.Is(topic), Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                subscribedAction = ci.Arg<Action<Message>>();
                return subscribeId;
            });

        var instance = new TestSequenceBaseTestInstance(messagingMock);

        string? receivedPayload = null;
        
        using (var _ = instance.SubscribeAction(topic, pl => receivedPayload = pl))
        {
            Assert.NotNull(subscribedAction);

            subscribedAction!(new Message {Topic = topic, Payload = payload});

            Assert.Equal(payload, receivedPayload);
        }

        messagingMock.Received(1).Subscribe(Arg.Is(topic), Arg.Any<Action<Message>>());
        messagingMock.Received(1).Unsubscribe(Arg.Is<object>(subscribeId));
    }

    [Fact]
    public void PayloadToVariableHandlerTriggered()
    {
        const string topic = "testTopic";
        const string payload = "testPayload";

        var subscribeId = "1234";
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        messagingMock.Subscribe<IMessageHandler>(Arg.Is(topic))
            .Returns(ci => subscribeId);

        var instance = new TestSequenceBaseTestInstance(messagingMock);

        string? receivedPayload = null;
        using (var _ = instance.SubscribeAction<IMessageHandler>(topic, pl => receivedPayload = pl))
        {
            instance.Handle(new Message { Topic = topic, Payload = payload });

            Assert.Equal(payload, receivedPayload);
        }

        messagingMock.Received(1).Subscribe<IMessageHandler>(Arg.Is(topic));
        messagingMock.Received(1).Unsubscribe(Arg.Is<object>(subscribeId));
    }

    [Fact]
    public void WaitForValueReturnsAfterValueHasBeenSet()
    {
        const string payload = "testPayload";
        
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        var instance = new TestSequenceBaseTestInstance(messagingMock);

        string? variableToWaitFor = null;
        Task.Run(async () =>
        {
            await Task.Delay(50);
            variableToWaitFor = "testPayload";
        });

        instance.WaitForPayload(ref variableToWaitFor, 500);

        Assert.Equal(payload, variableToWaitFor);
    }

    [Fact]
    public void WaitForValueThrowsTimeoutExceptionAfterNoValueSet()
    {
        const string payload = "testPayload";

        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        var instance = new TestSequenceBaseTestInstance(messagingMock);

        string? variableToWaitFor = null;
        Assert.Throws<TimeoutException>(() => instance.WaitForPayload(ref variableToWaitFor, 1));

        Assert.Null(variableToWaitFor);
    }
}
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using NSubstitute;
using SAF.Communication.Cde.ConnectionTypes;
using SAF.Communication.Cde;
using System;
using Xunit;
using nsCDEngine.BaseClasses;
using SAF.Communication.PubSub.Interfaces;
using System.Threading.Tasks;
using SAF.Communication.PubSub.Cde.Authorization;

namespace SAF.Communication.PubSub.Cde.Tests
{
    public class TestCommunicationPubSubCde
    {
        [Fact]
        public async void RunPublisher()
        {
            var thing = new TheThing();
            var comLinePublisher = new TestDefaultComLine(thing);
            Publisher publisher = new(comLinePublisher);
            await publisher.ConnectAsync();

            var comLineSubscriber = new TestDefaultComLine(thing);
            comLineSubscriber.Opposite = comLinePublisher;
            comLinePublisher.Opposite = comLineSubscriber;
            Subscriber subscriber = new(comLineSubscriber, publisher);
            subscriber.Subscribe("test")
                .SetHandler((_, message) =>
                {
                    ;
                });

            publisher.Publish("test", "A new Message");
            publisher.Publish($"{AuthorizationService.BaseChannelName}/*", "Check");
        }

    }

    public class TestDefaultComLine : DefaultComLine
    {
        public TestDefaultComLine Opposite;
        public override event MessageReceivedHandler MessageReceived;

        public TestDefaultComLine(TheThing thing) : base(thing) { }

        public override async Task Subscribe(string engineName)
        {
            await Task.Delay(300);
        }

        public override void Broadcast(TSM message)
        {
            HandleMessage(message);
            Opposite.HandleMessage(message);
        }
        public override void AnswerToSender(TSM originalMessage, TSM reply)
        {
            Opposite.HandleMessage(reply);
        }

        public void HandleMessage(TSM msg)
        {
            TheProcessMessage tpm = new(msg);
            tpm.Message.ORG = this.Address;
            MessageReceived?.Invoke(null, tpm);
        }
    }
}

// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.PubSub;
using SAF.Communication.PubSub.Interfaces;
using System;
using System.Collections.Generic;
using Xunit;

namespace SAF.Messaging.Cde.Tests
{
    public class TestMessagingCde
    {
        [Fact]
        public void RunCdeMessagingConfiguration()
        {
            CdeMessagingConfiguration cmc = new();
            Assert.Equal(RoutingOptions.All, cmc.RoutingOptions);

            MessagingConfiguration mc = new();
            mc.Config = new Dictionary<string, string>();
            mc.Config.Add("routingOptions", "Remote");
            cmc = new(mc);
            Assert.Equal(RoutingOptions.Remote, cmc.RoutingOptions);
        }

        [Fact]
        public void RunMessaging()
        {
            IServiceMessageDispatcher smd = Substitute.For<IServiceMessageDispatcher>();
            IPublisher publisher = Substitute.For<IPublisher>();
            var comLineSubscriber = Substitute.For<ComLine>();
            ISubscriber subscriber = Substitute.For<ISubscriber>();
            var test = Substitute.For<AbstractSubscription>(subscriber, RoutingOptions.All, new string[] { "*" });
            subscriber.Subscribe(Arg.Any<RoutingOptions>(), Arg.Any<string>()).Returns(test);

            Messaging messaging = new(null, smd, publisher, subscriber, null);
            messaging.Unsubscribe(null);
            test.DidNotReceive().Unsubscribe();
            messaging.Unsubscribe("");
            test.DidNotReceive().Unsubscribe();
            messaging.Unsubscribe(test.Id);
            test.DidNotReceive().Unsubscribe();

            IMessageHandler messageHandler = Substitute.For<IMessageHandler>();
            messageHandler.CanHandle(Arg.Any<Message>()).Returns(true);
            string id = messaging.Subscribe<IMessageHandler>().ToString();
            Assert.Equal(test.Id.ToString(), id);
            Assert.Throws<ArgumentException>(() => messaging.Subscribe<IMessageHandler>("*"));
            messaging.Unsubscribe(null);
            test.DidNotReceive().Unsubscribe();
            messaging.Unsubscribe("");
            test.DidNotReceive().Unsubscribe();
            messaging.Unsubscribe("xxx");
            test.DidNotReceive().Unsubscribe();

            Message message = new();
            messaging.Publish(message);
            publisher.Received().Publish(Arg.Is<Message>(m => m.Equals(message)), Arg.Is<RoutingOptions>(r => r.Equals(RoutingOptions.All)));
            publisher.ClearReceivedCalls();

            messaging.Unsubscribe(test.Id.ToString());
            test.Received().Unsubscribe();
            test.ClearReceivedCalls();
        }

        [Fact]
        public void RunServices()
        {
            TestConfigurationProdvider cp = new();
            cp.Set("Cde:ScopeId", "321123");
            cp.Set("Logging:Console:IncludeScopes", "false");
            cp.Set("Logging:Console:LogLevel:Default", "Debug");
            cp.Set("Logging:LogLevel:Default", "Debug");
            //        "Cde": {
            //"UseRandomScopeId": false,
            //"ScopeId": "321123",
            //"ApplicationId": "/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?",
            //"StorageId": "{92DD9020-1604-480F-99D1-889E30DB4344}",
            //"ApplicationName": "SAF Test Host",
            //"ApplicationTitle": "SAF Test Host",
            //"PortalTitle": "Host Portal",
            //"DebugLevel": "3",
            //"HttpPort": 8080,
            //"WsPort": 8081,
            //"DontVerifyTrust": true,
            //"UseUserMapper": true,
            //"UseRandomDeviceId": false,
            //"FailOnAdminCheck": false,
            //"IsCloudService": false,
            //"AllowLocalHost": true,
            //"LogIgnore": "UPnP;cdeSniffer;WSQueuedSender;CoreComm;QueuedSender;QSRegistry",
            //"PreShutDownDelay": 5000
            //}
            IConfigurationRoot cr = new ConfigurationRoot(new List<IConfigurationProvider> { cp });

            var applicationServices = new ServiceCollection();
            applicationServices.AddLogging(l => l.AddConfiguration(cr.GetSection("Logging")).AddConsole());

            using var baseServiceProvider = applicationServices.BuildServiceProvider();
            var mainLogger = baseServiceProvider.GetService<ILogger<TestMessagingCde>>();
            mainLogger.LogInformation("Starting test runner console app...");
            System.Threading.Thread.Sleep(3);
            applicationServices.AddCdeInfrastructure(cr.GetSection("Cde").Bind);
        }

        internal class TestConfigurationProdvider : ConfigurationProvider
        {
            public TestConfigurationProdvider() : base()
            { }
        }
    }
}

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestUtilities;
using Xunit;

namespace SAF.Messaging.InProcess.Tests
{
    public class MessagingBasicTests
    {
        [Fact]
        public async Task ExactMatchHits()
        {
            var dispatcher = new MessageDispatcher(NullLogger<MessageDispatcher>.Instance);
            var sut = new InProcessMessaging(NullLogger<InProcessMessaging>.Instance, dispatcher);
            bool hit = false;

            sut.Subscribe("a/test/channel/123", m => hit = true);
            sut.Publish(new Message { Topic = "a/test/channel/123" });

            await WaitUtils.WaitUntil(() => hit);
            Assert.True(hit);
        }

        [Fact]
        public async Task WildcardMatchHits()
        {
            var dispatcher = new MessageDispatcher(NullLogger<MessageDispatcher>.Instance);
            var sut = new InProcessMessaging(NullLogger<InProcessMessaging>.Instance, dispatcher);
            bool hit = false;

            sut.Subscribe(m => hit = true);
            sut.Publish(new Message { Topic = "something/completly/different" });

            await WaitUtils.WaitUntil(() => hit);
            Assert.True(hit);
        }

        [Fact]
        public async Task PublishDoesntBlock()
        {
            var dispatcher = new MessageDispatcher(NullLogger<MessageDispatcher>.Instance);
            var sut = new InProcessMessaging(NullLogger<InProcessMessaging>.Instance, dispatcher);
            DateTimeOffset publishContinued, subscriptionHit = DateTimeOffset.MinValue;

            sut.Subscribe("a/test/channel/123", m => 
            {
                Thread.Sleep(200);
                subscriptionHit = DateTimeOffset.Now;
            });

            sut.Publish(new Message { Topic = "a/test/channel/123" });
            publishContinued = DateTimeOffset.Now;

            await WaitUtils.WaitUntil(() => subscriptionHit > DateTimeOffset.MinValue);
            Assert.True(publishContinued < subscriptionHit);
        }
    }
}

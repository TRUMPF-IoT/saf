// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests;

using NSubstitute;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.Toolbox.RequestClient;
using Xunit;

public class ReplyTopicsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IMessagingInfrastructure _messaging = Substitute.For<IMessagingInfrastructure>();

    [Theory]
    [InlineData("private/reply")]
    [InlineData("private/reply/")]
    [InlineData("p")]
    public void Create_ReturnsATopicTheSubscriptionMatches(string prefix)
    {
        string? pattern = null;
        _messaging.Subscribe(Arg.Do<string>(p => pattern = p), Arg.Any<Action<Message>>());
        var sut = new ReplyTopics(_messaging, _ => { });

        var topic = sut.Create(prefix);

        Assert.StartsWith(prefix.TrimEnd('/') + "/", topic);
        Assert.StartsWith("*/requestclient/", pattern);
        Assert.True(topic.IsMatch(pattern!));
    }

    [Fact]
    public void Create_ReturnsUniqueTopics_PerRequestAndClient()
    {
        var sut = new ReplyTopics(_messaging, _ => { });
        var other = new ReplyTopics(_messaging, _ => { });

        var topics = new[] { sut.Create("p"), sut.Create("p"), other.Create("p") };

        Assert.Equal(3, topics.Distinct().Count());
        Assert.NotEqual(topics[0][topics[0].LastIndexOf('/')..], topics[2][topics[2].LastIndexOf('/')..]);
    }

    [Fact]
    public void Create_SubscribesOnceForEveryPrefix()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        sut.Create("a/b");
        sut.Create("a/b/");
        sut.Create("c");

        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Create_SubscribesOnce_WhenCalledConcurrently()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        Parallel.For(0, 50, i => sut.Create($"p{i % 5}"));

        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Create_SubscribesTheReplyHandler()
    {
        Action<Message>? subscribed = null;
        _messaging.Subscribe(Arg.Any<string>(), Arg.Do<Action<Message>>(h => subscribed = h));
        Message? handled = null;
        var sut = new ReplyTopics(_messaging, m => handled = m);
        var reply = new Message { Topic = sut.Create("p") };

        subscribed!(reply);

        Assert.Same(reply, handled);
    }

    [Fact]
    public void Create_SubscribesAgain_AfterAFailedSubscription()
    {
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>())
            .Returns(_ => throw new InvalidOperationException("subscribe failed"), _ => new object());
        var sut = new ReplyTopics(_messaging, _ => { });

        Assert.Throws<InvalidOperationException>(() => sut.Create("p"));
        var topic = sut.Create("p");

        Assert.StartsWith("p/", topic);
        _messaging.Received(2).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Subscribe_SubscribesAheadOfTheFirstRequest()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        sut.Subscribe();
        _messaging.Received(1).Subscribe(Arg.Is<string>(p => p.StartsWith("*/requestclient/")), Arg.Any<Action<Message>>());
        sut.Create("p");

        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public async Task Create_WaitsForTheSubscriptionInProgress()
    {
        using var subscribing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>())
            .Returns(_ => { subscribing.Set(); release.Wait(Timeout, TestContext.Current.CancellationToken); return new object(); });
        var sut = new ReplyTopics(_messaging, _ => { });

        var first = Task.Run(() => sut.Create("a"), TestContext.Current.CancellationToken);
        Assert.True(subscribing.Wait(Timeout, TestContext.Current.CancellationToken));
        var second = Task.Run(() => sut.Create("b"), TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);
        release.Set();
        await Task.WhenAll(first, second).WaitAsync(Timeout, TestContext.Current.CancellationToken);
        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Dispose_UnsubscribesOnce()
    {
        var handle = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(handle);
        var sut = new ReplyTopics(_messaging, _ => { });
        sut.Create("a");
        sut.Create("b");

        sut.Dispose();
        sut.Dispose();

        _messaging.Received(1).Unsubscribe(handle);
        _messaging.Received(1).Unsubscribe(Arg.Any<object>());
    }

    [Fact]
    public void Dispose_DoesNotSubscribe_WithoutAnyRequest()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        sut.Dispose();

        _messaging.DidNotReceive().Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
        _messaging.DidNotReceive().Unsubscribe(Arg.Any<object>());
    }

    [Fact]
    public void Dispose_SkipsAFailedSubscription()
    {
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(_ => throw new InvalidOperationException());
        var sut = new ReplyTopics(_messaging, _ => { });
        Assert.Throws<InvalidOperationException>(() => sut.Create("p"));

        sut.Dispose();

        _messaging.DidNotReceive().Unsubscribe(Arg.Any<object>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dispose_WaitsForTheSubscriptionInProgress(bool succeeds)
    {
        var handle = new object();
        using var subscribing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>())
            .Returns(_ =>
            {
                subscribing.Set();
                release.Wait(Timeout, TestContext.Current.CancellationToken);
                return succeeds ? handle : throw new InvalidOperationException("subscribe failed");
            });
        var sut = new ReplyTopics(_messaging, _ => { });
        var request = Task.Run(() => sut.Create("p"), TestContext.Current.CancellationToken);
        Assert.True(subscribing.Wait(Timeout, TestContext.Current.CancellationToken));

        var dispose = Task.Run(sut.Dispose, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(dispose.IsCompleted);
        release.Set();

        await dispose.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        if (succeeds)
        {
            await request.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            _messaging.Received(1).Unsubscribe(handle);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => request.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            _messaging.DidNotReceive().Unsubscribe(Arg.Any<object>());
        }
    }
}

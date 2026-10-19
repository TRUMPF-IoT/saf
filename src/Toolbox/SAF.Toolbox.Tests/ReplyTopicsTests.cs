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
    public void Create_ReturnsATopicTheSubscriptionMatchesInExactlyOneLevel(string prefix)
    {
        string? pattern = null;
        _messaging.Subscribe(Arg.Do<string>(p => pattern = p), Arg.Any<Action<Message>>());
        var sut = new ReplyTopics(_messaging, _ => { });

        var topic = sut.Create(prefix);

        Assert.StartsWith(prefix.TrimEnd('/') + "/", topic);
        Assert.True(topic.IsMatch(pattern!));
        // NATS matches '*' against exactly one level, so the topic must have exactly one level in its place.
        var patternLevels = pattern!.Split('/');
        var topicLevels = topic.Split('/');
        var wildcard = Array.IndexOf(patternLevels, "*");
        Assert.True(Guid.TryParse(topicLevels[wildcard], out _));
        patternLevels[wildcard] = topicLevels[wildcard];
        Assert.Equal(topicLevels, patternLevels);
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
    public void Create_SubscribesOncePerPrefix()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        sut.Create("a/b");
        sut.Create("a/b/");
        sut.Create("c");

        _messaging.Received(1).Subscribe(Arg.Is<string>(p => p.StartsWith("a/b/*/requestclient/")), Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe(Arg.Is<string>(p => p.StartsWith("c/*/requestclient/")), Arg.Any<Action<Message>>());
        _messaging.Received(2).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Create_SubscribesOnce_WhenCalledConcurrently()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        Parallel.For(0, 50, _ => sut.Create("p"));

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
    public void Subscribe_SubscribesThePrefixAheadOfTheFirstRequest()
    {
        var sut = new ReplyTopics(_messaging, _ => { });

        sut.Subscribe("p/");
        _messaging.Received(1).Subscribe(Arg.Is<string>(p => p.StartsWith("p/*/requestclient/")), Arg.Any<Action<Message>>());
        sut.Create("p");

        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public async Task Create_DoesNotWaitForTheSubscriptionOfAnotherPrefix()
    {
        using var subscribing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _messaging.Subscribe(Arg.Is<string>(p => p.StartsWith("slow/")), Arg.Any<Action<Message>>())
            .Returns(_ => { subscribing.Set(); release.Wait(Timeout, TestContext.Current.CancellationToken); return new object(); });
        var sut = new ReplyTopics(_messaging, _ => { });

        var slow = Task.Run(() => sut.Create("slow"), TestContext.Current.CancellationToken);
        Assert.True(subscribing.Wait(Timeout, TestContext.Current.CancellationToken));
        var fast = Task.Run(() => sut.Create("fast"), TestContext.Current.CancellationToken);

        Assert.StartsWith("fast/", await fast.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.False(slow.IsCompleted);
        release.Set();
        Assert.StartsWith("slow/", await slow.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_WaitsForTheSubscriptionOfItsPrefix()
    {
        using var subscribing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>())
            .Returns(_ => { subscribing.Set(); release.Wait(Timeout, TestContext.Current.CancellationToken); return new object(); });
        var sut = new ReplyTopics(_messaging, _ => { });

        var first = Task.Run(() => sut.Create("p"), TestContext.Current.CancellationToken);
        Assert.True(subscribing.Wait(Timeout, TestContext.Current.CancellationToken));
        var second = Task.Run(() => sut.Create("p"), TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);
        release.Set();
        await Task.WhenAll(first, second).WaitAsync(Timeout, TestContext.Current.CancellationToken);
        _messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Fact]
    public void Dispose_UnsubscribesEverySubscription()
    {
        var first = new object();
        var second = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(first, second, null!);
        var sut = new ReplyTopics(_messaging, _ => { });
        sut.Create("a");
        sut.Create("b");
        sut.Create("c");

        sut.Dispose();
        sut.Dispose();

        _messaging.Received(1).Unsubscribe(first);
        _messaging.Received(1).Unsubscribe(second);
        _messaging.Received(2).Unsubscribe(Arg.Any<object>());
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
}

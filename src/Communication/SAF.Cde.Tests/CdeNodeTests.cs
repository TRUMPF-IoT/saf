// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TestUtilities;
using Xunit;

public class CdeNodeTests
{
    private readonly FakeRuntimeFactory _runtimeFactory = new();
    private readonly FakeOwnership _ownership = new();

    [Fact]
    public void Acquire_StartsTheNodeForTheFirstLeaseOnly()
    {
        var node = CreateNode();

        using var first = node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance);
        using var second = node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance);

        Assert.Single(_runtimeFactory.Started);
        Assert.Equal(1, _ownership.ClaimCount);
    }

    [Fact]
    public void Acquire_PassesTheConfigurationAndLoggerFactoryToTheStart()
    {
        var configuration = new CdeConfiguration { ScopeId = "4711" };
        var node = CreateNode();

        using var lease = node.Acquire(configuration, NullLoggerFactory.Instance);

        Assert.Same(configuration, _runtimeFactory.StartedWith);
        Assert.Same(NullLoggerFactory.Instance, _runtimeFactory.StartedWithLoggerFactory);
    }

    [Fact]
    public void Acquire_DoesNotStartTheNode_WhenAnotherCopyRunsCde()
    {
        _ownership.OwnedByAnotherCopy = true;
        var node = CreateNode();

        Assert.Throws<InvalidOperationException>(() => node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance));

        Assert.Empty(_runtimeFactory.Started);
    }

    [Fact]
    public void Release_ShutsTheNodeDownWithTheLastLeaseOnly()
    {
        var node = CreateNode();
        var first = node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance);
        var second = node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance);

        first.Dispose();
        Assert.False(_runtimeFactory.Started[0].IsDisposed);

        second.Dispose();
        Assert.True(_runtimeFactory.Started[0].IsDisposed);
    }

    [Fact]
    public void Acquire_Throws_AfterTheLastLeaseShutTheNodeDown()
    {
        var node = CreateNode();
        node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance).Dispose();

        var exception = Assert.Throws<InvalidOperationException>(() => node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance));

        Assert.Contains("restart the host", exception.Message, StringComparison.Ordinal);
        Assert.Single(_runtimeFactory.Started);
    }

    [Fact]
    public void Acquire_RetriesTheStart_WhenTheFirstStartFailed()
    {
        _runtimeFactory.NextStartFailure = new InvalidOperationException("Start failed.");
        var node = CreateNode();

        Assert.Throws<InvalidOperationException>(() => node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance));
        using var lease = node.Acquire(new CdeConfiguration(), NullLoggerFactory.Instance);

        Assert.Single(_runtimeFactory.Started);
    }

    [Fact]
    public void Acquire_Warns_WhenTheNodeRunsWithADifferentConfiguration()
    {
        var logger = Substitute.For<MockLogger>();
        var node = CreateNode(new FixedComparer(areEqual: false));

        using var first = node.Acquire(new CdeConfiguration(), new SingleLoggerFactory(logger));
        using var second = node.Acquire(new CdeConfiguration(), new SingleLoggerFactory(logger));

        logger.AssertLogged(LogLevel.Warning, message => message.Contains("different configuration", StringComparison.Ordinal));
    }

    [Fact]
    public void Acquire_DoesNotWarn_WhenTheNodeRunsWithTheSameConfiguration()
    {
        var logger = Substitute.For<MockLogger>();
        var node = CreateNode(new FixedComparer(areEqual: true));

        using var first = node.Acquire(new CdeConfiguration(), new SingleLoggerFactory(logger));
        using var second = node.Acquire(new CdeConfiguration(), new SingleLoggerFactory(logger));

        logger.AssertNotLogged(LogLevel.Warning);
    }

    private CdeNode CreateNode(IEqualityComparer<CdeConfiguration>? configurationComparer = null)
        => new(_runtimeFactory, _ownership, configurationComparer ?? new FixedComparer(areEqual: true));

    private sealed class FakeRuntimeFactory : ICdeRuntimeFactory
    {
        public List<FakeRuntime> Started { get; } = [];
        public CdeConfiguration? StartedWith { get; private set; }
        public ILoggerFactory? StartedWithLoggerFactory { get; private set; }
        public Exception? NextStartFailure { get; set; }

        public IDisposable Start(CdeConfiguration configuration, ILoggerFactory loggerFactory)
        {
            if (NextStartFailure is { } failure)
            {
                NextStartFailure = null;
                throw failure;
            }

            StartedWith = configuration;
            StartedWithLoggerFactory = loggerFactory;

            var runtime = new FakeRuntime();
            Started.Add(runtime);
            return runtime;
        }
    }

    private sealed class FakeRuntime : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeOwnership : ICdeNodeOwnership
    {
        public int ClaimCount { get; private set; }
        public bool OwnedByAnotherCopy { get; set; }

        public void Claim()
        {
            ClaimCount++;
            if (OwnedByAnotherCopy)
            {
                throw new InvalidOperationException("Another copy of SAF.Cde runs C-DEngine.");
            }
        }
    }

    private sealed class FixedComparer(bool areEqual) : IEqualityComparer<CdeConfiguration>
    {
        public bool Equals(CdeConfiguration? x, CdeConfiguration? y) => areEqual;

        public int GetHashCode(CdeConfiguration obj) => 0;
    }
}
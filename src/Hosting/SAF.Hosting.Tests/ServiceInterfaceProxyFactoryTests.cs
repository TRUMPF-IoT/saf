// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using System;
using System.Threading.Tasks;
using NSubstitute;
using SAF.Hosting;
using Xunit;

public class ServiceInterfaceProxyFactoryTests
{
    public interface ITestService
    {
        int Add(int a, int b);
        Task<int> AddAsync(int a, int b);
        bool ABooleanProperty { get; set; }
    }

#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    // Disposable test service implementation
    public class TestServiceDisposable : ITestService, IDisposable
    {
        public bool IsDisposed { get; private set; }
        
        public int Add(int a, int b) => a + b;
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
        public bool ABooleanProperty { get; set; }

        public void Dispose() => IsDisposed = true;
    }

    public class TestServiceAsyncDisposable : ITestService, IAsyncDisposable
    {
        public bool IsDisposedAsync { get; private set; }
        
        public int Add(int a, int b) => a + b;
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
        public bool ABooleanProperty { get; set; }

        public ValueTask DisposeAsync()
        {
            IsDisposedAsync = true;
            return ValueTask.CompletedTask;
        }
    }

    public class ThrowingService : ITestService, IDisposable
    {
        public int Add(int a, int b) => throw new InvalidOperationException("boom");
        public Task<int> AddAsync(int a, int b) => throw new InvalidOperationException("boom-async");
        
        public bool ABooleanProperty
        {
            get => throw new InvalidOperationException("boom-property-get");
            set => throw new InvalidOperationException("boom-property-set");
        }

        public void Dispose() {}
    }
#pragma warning restore S3881 // "IDisposable" should be implemented correctly

    private class TestServiceNonDisposable : ITestService
    {
        public int Add(int a, int b) => a + b;
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
        public bool ABooleanProperty { get; set; }
    }

    [Fact]
    public void Create_ReturnsOriginal_WhenNotDisposable()
    {
        ITestService target = new TestServiceNonDisposable();

        var result = ServiceInterfaceProxyFactory.Create(target);

        Assert.Same(target, result);
    }

    [Fact]
    public void Create_ReturnsProxy_DelegatesMethods()
    {
        ITestService target = Substitute.For<TestServiceDisposable>();

        var result = ServiceInterfaceProxyFactory.Create(target);

        Assert.NotSame(target, result);
        Assert.Equal(5, result.Add(2, 3));
        target.Received(1).Add(2, 3);
    }

    [Fact]
    public void Create_ReturnsProxy_DelegatesProperties()
    {
        ITestService target = new TestServiceDisposable();

        var result = ServiceInterfaceProxyFactory.Create(target);
        
        Assert.NotSame(target, result);

        result.ABooleanProperty = true;
        Assert.True(target.ABooleanProperty);
        result.ABooleanProperty = false;
        Assert.False(target.ABooleanProperty);
    }

    [Fact]
    public async Task Create_ReturnsProxy_DelegatesAsyncMethods()
    {
        ITestService target = new TestServiceAsyncDisposable();

        var result = ServiceInterfaceProxyFactory.Create(target);

        Assert.NotSame(target, result);
        var sum = await result.AddAsync(4, 6);
        Assert.Equal(10, sum);
    }

    [Fact]
    public void Create_ThrowsArgumentException_WhenTIsNotInterface()
    {
        var target = new TestServiceDisposable();

        var ex = Assert.Throws<ArgumentException>(() => ServiceInterfaceProxyFactory.Create(target));
        Assert.Contains("must be an interface", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Proxy_UnwrapsInnerExceptions_Sync()
    {
        ITestService target = new ThrowingService();
        var proxy = ServiceInterfaceProxyFactory.Create(target);

        var ex = Assert.Throws<InvalidOperationException>(() => proxy.Add(1, 2));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Proxy_UnwrapsInnerExceptions_Async()
    {
        ITestService target = new ThrowingService();
        var proxy = ServiceInterfaceProxyFactory.Create(target);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await proxy.AddAsync(1, 2));
        Assert.Equal("boom-async", ex.Message);
    }

    [Fact]
    public void Proxy_UnwrapsInnerExceptions_FromPropertyGetter()
    {
        ITestService target = new ThrowingService();
        var proxy = ServiceInterfaceProxyFactory.Create(target);

        var ex = Assert.Throws<InvalidOperationException>(() => proxy.ABooleanProperty);
        Assert.Equal("boom-property-get", ex.Message);
    }

    [Fact]
    public void Proxy_UnwrapsInnerExceptions_Proxy_UnwrapsInnerExceptions_FromPropertySetter()
    {
        ITestService target = new ThrowingService();
        var proxy = ServiceInterfaceProxyFactory.Create(target);

        var ex = Assert.Throws<InvalidOperationException>(() => proxy.ABooleanProperty = true);
        Assert.Equal("boom-property-set", ex.Message);
    }
}

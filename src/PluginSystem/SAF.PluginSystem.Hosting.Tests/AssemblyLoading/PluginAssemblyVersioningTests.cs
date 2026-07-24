// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

/// <summary>
/// End-to-end assembly loading tests with two real, differently-versioned builds of the same shared
/// dependency (<c>TestPlugin.SharedVersioned</c> V1 and V2). The plugin (<c>TestPlugin.PluginVersioned</c>)
/// is compiled against V1 and ships V1 in its own folder; V2 stands in for the version the host provides.
/// </summary>
public class PluginAssemblyVersioningTests(HostSharedV2Fixture hostSharedV2)
    : IClassFixture<HostSharedV2Fixture>
{
    private const string SharedSimpleName = "TestPlugin.SharedVersioned";
    private const string PluginEntryTypeName = "TestPlugin.PluginVersioned.PluginVersionedEntry";

    private static string PluginPath =>
        Path.Combine(AppContext.BaseDirectory, "plugins", "TestPlugin.PluginVersioned", "TestPlugin.PluginVersioned.dll");

    [Fact]
    public void SharedDependency_RollsForwardToHostVersion_WhenShared()
    {
        // The host provides V2 in the default context.
        var hostShared = hostSharedV2.Assembly;
        Assert.Equal(2, hostShared.GetName().Version!.Major);

        var context = CreateContext(SharedAssemblyDecision.ShareFromDefault, new Version(2, 0, 0, 0), SharedAssemblyConflictBehavior.Fail);
        var plugin = context.LoadFromAssemblyPath(PluginPath);

        // The plugin was compiled against V1 but transparently binds to the host's V2 across the boundary.
        Assert.Equal(2, InvokeGetSharedMajorVersion(plugin));
        var sharedFromPlugin = InvokeGetSharedAssembly(plugin);
        Assert.Same(hostShared, sharedFromPlugin);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(sharedFromPlugin));
    }

    [Fact]
    public void SharedDependency_LoadsPluginPrivateVersion_WhenIsolated()
    {
        var context = CreateContext(SharedAssemblyDecision.LoadIsolated, hostVersion: null, SharedAssemblyConflictBehavior.Fail);
        var plugin = context.LoadFromAssemblyPath(PluginPath);

        // Not shared: the plugin uses the V1 copy shipped in its own folder, loaded in its own context.
        Assert.Equal(1, InvokeGetSharedMajorVersion(plugin));
        var sharedFromPlugin = InvokeGetSharedAssembly(plugin);
        Assert.Same(context, AssemblyLoadContext.GetLoadContext(sharedFromPlugin));
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(sharedFromPlugin));
    }

    [Fact]
    public void SharedDependency_V1AndV2_CoexistInSeparateContexts()
    {
        var hostShared = hostSharedV2.Assembly;

        var isolatedContext = CreateContext(SharedAssemblyDecision.LoadIsolated, hostVersion: null, SharedAssemblyConflictBehavior.Fail);
        var plugin = isolatedContext.LoadFromAssemblyPath(PluginPath);
        var isolatedShared = InvokeGetSharedAssembly(plugin);

        Assert.Equal(2, hostShared.GetName().Version!.Major);
        Assert.Equal(1, isolatedShared.GetName().Version!.Major);
        Assert.NotSame(hostShared, isolatedShared);
    }

    [Fact]
    public void SharedDependency_Conflict_Fails_WithClearException()
    {
        var context = CreateContext(SharedAssemblyDecision.Conflict, new Version(1, 0, 0, 0), SharedAssemblyConflictBehavior.Fail);

        var requested = new AssemblyName(SharedSimpleName) { Version = new Version(2, 0, 0, 0) };

        // The runtime wraps any exception thrown from an AssemblyLoadContext.Load callback in a
        // FileLoadException; our clear diagnostic is preserved as the inner exception.
        var fileLoadException = Assert.Throws<FileLoadException>(() => context.LoadFromAssemblyName(requested));
        var exception = Assert.IsType<SharedAssemblyVersionConflictException>(fileLoadException.InnerException);

        Assert.Equal(SharedSimpleName, exception.SharedAssemblyName);
        Assert.Equal(new Version(2, 0, 0, 0), exception.RequestedVersion);
        Assert.Equal(new Version(1, 0, 0, 0), exception.HostVersion);
    }

    [Fact]
    public void SharedDependency_Conflict_LoadsIsolated_WhenBehaviorIsIsolateWithWarning()
    {
        var context = CreateContext(SharedAssemblyDecision.Conflict, new Version(1, 0, 0, 0), SharedAssemblyConflictBehavior.IsolateWithWarning);
        var plugin = context.LoadFromAssemblyPath(PluginPath);

        // Best effort: the plugin's own V1 is loaded in isolation instead of failing.
        Assert.Equal(1, InvokeGetSharedMajorVersion(plugin));
        Assert.Same(context, AssemblyLoadContext.GetLoadContext(InvokeGetSharedAssembly(plugin)));
    }

    private static PluginAssemblyLoadContext CreateContext(
        SharedAssemblyDecision decision,
        Version? hostVersion,
        SharedAssemblyConflictBehavior conflictBehavior)
        => new(
            NullLoggerFactory.Instance,
            PluginPath,
            new StubSharedAssemblyResolver(SharedSimpleName, decision, hostVersion),
            conflictBehavior);

    private static Assembly InvokeGetSharedAssembly(Assembly plugin)
        => (Assembly)InvokeEntry(plugin, "GetSharedAssembly")!;

    private static int InvokeGetSharedMajorVersion(Assembly plugin)
        => (int)InvokeEntry(plugin, "GetSharedMajorVersion")!;

    private static object? InvokeEntry(Assembly plugin, string methodName)
    {
        var entryType = plugin.GetType(PluginEntryTypeName)!;
        var method = entryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        return method.Invoke(null, null);
    }

    /// <summary>Returns a fixed decision for the shared assembly under test and isolates everything else.</summary>
    private sealed class StubSharedAssemblyResolver(string simpleName, SharedAssemblyDecision decision, Version? hostVersion)
        : ISharedAssemblyResolver
    {
        public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? host)
        {
            if (string.Equals(requested.Name, simpleName, StringComparison.OrdinalIgnoreCase))
            {
                host = hostVersion;
                return decision;
            }

            host = null;
            return SharedAssemblyDecision.LoadIsolated;
        }
    }
}

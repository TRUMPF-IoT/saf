// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

/// <summary>
/// Provides a custom plugin configuration source callback with everything the built-in plugin settings
/// pipeline already knows, so the callback can add sources that follow the same conventions (settings
/// directory, environment overlay naming, load-exception handling) without re-deriving them.
/// </summary>
public sealed class PluginConfigurationSourceContext
{
    /// <summary>
    /// The configuration builder the callback should append its providers to.
    /// </summary>
    public required IConfigurationBuilder Builder { get; init; }

    /// <summary>
    /// The <see cref="IFileProvider"/> scoped to the resolved plugin settings directory, i.e. the same
    /// provider the default plugin settings JSON file(s) are loaded through. <see langword="null"/> if
    /// <see cref="PluginSystemOptions.PluginSettingsFilePath"/> is not configured.
    /// </summary>
    public required IFileProvider? SettingsFileProvider { get; init; }

    /// <summary>
    /// The host environment name (e.g. "Development", "Production").
    /// </summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// The file name (without directory) of the default plugin settings file, e.g. "pluginsettings.json".
    /// <see langword="null"/> if <see cref="PluginSystemOptions.PluginSettingsFilePath"/> is not configured.
    /// </summary>
    public required string? SettingsFileName { get; init; }

    /// <summary>
    /// The shared load-exception handler the default plugin settings sources use: it ignores the failure
    /// and logs a warning so a malformed or half-written file neither crashes host startup nor silently
    /// wipes values on reload. Assign it to a custom <see cref="FileConfigurationSource.OnLoadException"/>
    /// to get the same behavior.
    /// </summary>
    public required Action<FileLoadExceptionContext> OnLoadException { get; init; }

    private readonly List<Func<IConfigurationRoot, IConfigurationRoot>> _configurationRootDecorators = [];

    /// <summary>
    /// Registers a decorator that is applied to the plugin configuration root after every source has
    /// been added and built. Use it for a provider that has to read the <em>composed</em> configuration,
    /// such as one that resolves or overlays values: such a provider cannot work as a peer source,
    /// because the composition it needs does not exist until all sources are built.
    /// </summary>
    /// <param name="decorator">
    /// Receives the root built from all sources and returns the root the host will use. Returning a new
    /// root transfers ownership of the one passed in, so the returned root must dispose it.
    /// </param>
    /// <remarks>
    /// Decorators are applied in registration order, each wrapping the result of the previous one.
    /// </remarks>
    public void DecorateConfigurationRoot(Func<IConfigurationRoot, IConfigurationRoot> decorator)
    {
        ArgumentNullException.ThrowIfNull(decorator);

        _configurationRootDecorators.Add(decorator);
    }

    internal IConfigurationRoot ApplyConfigurationRootDecorators(IConfigurationRoot root)
    {
        foreach (var decorator in _configurationRootDecorators)
        {
            root = decorator(root)
                ?? throw new InvalidOperationException(
                    $"A {nameof(DecorateConfigurationRoot)} callback returned null. It must return the "
                    + "configuration root the host should use.");
        }

        return root;
    }

    /// <summary>
    /// The host's <see cref="IServiceProvider"/>. It is fully built by the time this context is created,
    /// so a host-registered service can be resolved from it here — except one that itself depends on
    /// <see cref="IPluginSystemHostContext"/>, which is the service these callbacks are running inside.
    /// Resolving such a service (<c>IPluginServiceProvider</c>, <c>IPluginSystemController</c> and the
    /// host context itself, among others) throws an <see cref="InvalidOperationException"/> naming it,
    /// rather than being left to the container, which re-enters its own factory until the process dies of
    /// a StackOverflowException. The refusal comes from the host built by <c>AddPluginSystem</c>; a
    /// directly constructed <see cref="PluginSystemHostContext"/> hands the callbacks whatever provider it
    /// was given.
    /// </summary>
    public required IServiceProvider HostServices { get; init; }
}

// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Logging;

internal sealed class NonOwningLoggerFactory(ILoggerFactory innerLoggerFactory) : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        innerLoggerFactory.AddProvider(provider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("The logger category name must not be null, empty, or whitespace.", nameof(categoryName));
        }

        return innerLoggerFactory.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        // Intentionally no-op. The host owns the wrapped ILoggerFactory lifecycle.
    }
}

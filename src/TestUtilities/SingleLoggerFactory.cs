// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps a single <see cref="ILogger"/> - typically a <see cref="MockLogger"/> substitute - as an
/// <see cref="ILoggerFactory"/> that returns it for every category, so a type built through an
/// <see cref="ILoggerFactory"/> constructor parameter can still be asserted on with
/// <see cref="MockLoggerAssertionExtensions"/>.
/// </summary>
public sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => logger;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

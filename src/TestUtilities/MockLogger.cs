// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;

namespace TestUtilities;

public abstract class MockLogger : ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Log(logLevel, formatter(state, exception));

    public abstract void Log(LogLevel level, string message);

    public abstract bool IsEnabled(LogLevel logLevel);
    public abstract IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

public abstract class MockLogger<T> : MockLogger, ILogger<T>
{
        
}
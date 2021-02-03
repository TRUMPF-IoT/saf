using System;
using Microsoft.Extensions.Logging;

namespace TestUtilities
{
    public abstract class MockLogger : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => Log(logLevel, formatter(state, exception));

        public abstract void Log(LogLevel level, string message);

        public abstract bool IsEnabled(LogLevel logLevel);
        public abstract IDisposable BeginScope<TState>(TState state);
    }

    public abstract class MockLogger<T> : MockLogger, ILogger<T>
    {
        
    }
}
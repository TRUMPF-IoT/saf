// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace SAF.Toolbox.Tests;

internal class TestLogger : ILogger
{
    private readonly ITestOutputHelper _outputHelper;

    public TestLogger(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _outputHelper.WriteLine($"{state}");
        if (exception != null)
            _outputHelper.WriteLine(exception.ToString());
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) => new Scope();

    private class Scope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal class TestLogger<T> : TestLogger, ILogger<T>
{
    public TestLogger(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }
}
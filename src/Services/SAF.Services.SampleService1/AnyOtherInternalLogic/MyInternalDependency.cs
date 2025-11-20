// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1.AnyOtherInternalLogic;
using Microsoft.Extensions.Logging;

internal class MyInternalDependency
{
    private readonly ILogger<MyInternalDependency> _log;

    public MyInternalDependency(ILogger<MyInternalDependency> log)
    {
        _log = log;
    }

    public void SayHello()
    {
        _log.LogInformation("Hello world, i'm an internal dependency, only visible within the SampleService1 assembly.");
    }
}
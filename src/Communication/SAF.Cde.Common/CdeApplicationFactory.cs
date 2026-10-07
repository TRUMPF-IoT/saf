// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Starts C-DEngine through <see cref="CdeApplication"/>.
/// </summary>
internal sealed class CdeApplicationFactory : ICdeRuntimeFactory
{
    public IDisposable Start(CdeConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var cdeApp = new CdeApplication(loggerFactory.CreateLogger<CdeApplication>(), configuration);
        cdeApp.Start();
        return cdeApp;
    }
}
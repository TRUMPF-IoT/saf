// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using Microsoft.Extensions.Logging;

/// <summary>
/// Starts C-DEngine.
/// </summary>
internal interface ICdeRuntimeFactory
{
    /// <summary>
    /// Starts C-DEngine with the given configuration.
    /// </summary>
    /// <returns>The running C-DEngine; disposing it shuts C-DEngine down.</returns>
    IDisposable Start(CdeConfiguration configuration, ILoggerFactory loggerFactory);
}
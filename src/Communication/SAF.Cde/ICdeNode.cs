// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using Microsoft.Extensions.Logging;

/// <summary>
/// The C-DEngine node of the process, shared by all plug-in containers that need it.
/// </summary>
internal interface ICdeNode
{
    /// <summary>
    /// Returns a lease on the node and starts the node if this is the first lease.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The node was already shut down, or another copy of SAF.Cde already runs C-DEngine in this process.
    /// </exception>
    CdeNodeLease Acquire(CdeConfiguration configuration, ILoggerFactory loggerFactory);
}
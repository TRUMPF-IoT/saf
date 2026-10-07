// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;

/// <summary>
/// Makes sure only one copy of SAF.Cde.Common runs C-DEngine in the process.
/// </summary>
internal interface ICdeNodeOwnership
{
    /// <summary>
    /// Records this copy of SAF.Cde.Common as the one that runs C-DEngine.
    /// </summary>
    /// <exception cref="InvalidOperationException">Another copy of SAF.Cde.Common already runs C-DEngine.</exception>
    void Claim();
}
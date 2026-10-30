// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;

internal interface ICdeStartArguments
{
    /// <summary>
    /// Creates the arguments C-DEngine is started with.
    /// </summary>
    IDictionary<string, string> Create(CdeConfiguration configuration);
}

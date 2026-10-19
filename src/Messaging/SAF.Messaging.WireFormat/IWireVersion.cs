// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

/// <summary>
/// Reads the major version of a wire format version. Formats are selected by major version: a minor version only
/// adds optional fields.
/// </summary>
public interface IWireVersion
{
    /// <returns><c>false</c> if the version cannot be read.</returns>
    bool TryGetMajor(string? version, out int major);
}

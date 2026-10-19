// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

/// <summary>
/// Versions in the form <c>major.minor[.build]</c>. A missing version is major version 1, the oldest formats
/// carried none.
/// </summary>
public sealed class WireVersion : IWireVersion
{
    public bool TryGetMajor(string? version, out int major)
    {
        if (string.IsNullOrEmpty(version))
        {
            major = 1;
            return true;
        }

        if (Version.TryParse(version, out var parsed))
        {
            major = parsed.Major;
            return true;
        }

        major = 0;
        return false;
    }
}

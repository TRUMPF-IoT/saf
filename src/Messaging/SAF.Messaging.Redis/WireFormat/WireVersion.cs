// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

/// <summary>
/// Formats are selected by major version: a minor version only adds optional fields.
/// </summary>
internal static class WireVersion
{
    public static bool TryGetMajor(string? version, out int major)
    {
        // The oldest format carried no version.
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

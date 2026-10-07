// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using System.Text.Json;

/// <summary>
/// Compares C-DEngine configurations by value, including the crypto library settings and the additional
/// arguments.
/// </summary>
internal sealed class CdeConfigurationComparer : IEqualityComparer<CdeConfiguration>
{
    public bool Equals(CdeConfiguration? x, CdeConfiguration? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x is not null && y is not null && Serialize(x) == Serialize(y);
    }

    public int GetHashCode(CdeConfiguration obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return StringComparer.Ordinal.GetHashCode(Serialize(obj));
    }

    private static string Serialize(CdeConfiguration configuration) => JsonSerializer.Serialize(configuration);
}
// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A parsed secret reference of the form <c>&lt;prefix&gt;&lt;name&gt;</c> (e.g. <c>secret://myproduct/conn-1/password</c>).
/// The reference is a pointer into the secret store and is not itself sensitive; it may appear in
/// configuration files, logs and source control. This helper is shared by the configuration
/// resolution provider and by product code that produces references, so the token format is defined once.
/// </summary>
public sealed class SecretReference
{
    private SecretReference(string prefix, string name)
    {
        Prefix = prefix;
        Name = name;
    }

    /// <summary>The prefix that marked the value as a reference.</summary>
    public string Prefix { get; }

    /// <summary>The logical secret name (the part after the prefix).</summary>
    public string Name { get; }

    /// <summary>
    /// Determines whether <paramref name="value"/> is a secret reference for the given <paramref name="prefix"/>.
    /// </summary>
    public static bool IsReference([NotNullWhen(true)] string? value, string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        return value is not null
            && value.Length > prefix.Length
            && value.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to parse <paramref name="value"/> as a secret reference for the given <paramref name="prefix"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the value is a non-empty reference; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, string prefix, [NotNullWhen(true)] out SecretReference? reference)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        reference = null;
        if (!IsReference(value, prefix))
        {
            return false;
        }

        var name = value[prefix.Length..];
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        reference = new SecretReference(prefix, name);
        return true;
    }

    /// <summary>
    /// Parses <paramref name="value"/> as a secret reference for the given <paramref name="prefix"/>.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the value is not a valid, non-empty reference.</exception>
    public static SecretReference Parse(string value, string prefix)
    {
        if (!TryParse(value, prefix, out var reference))
        {
            throw new FormatException($"The value is not a valid secret reference for prefix '{prefix}'.");
        }

        return reference;
    }

    /// <summary>
    /// Builds a reference token for <paramref name="name"/> using <paramref name="prefix"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> or <paramref name="name"/> is null/empty.</exception>
    public static string Build(string name, string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return prefix + name;
    }

    /// <summary>Returns the reference token (<see cref="Prefix"/> + <see cref="Name"/>).</summary>
    public override string ToString() => Prefix + Name;
}

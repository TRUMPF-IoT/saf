// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// Describes the configured messaging backend instance to use.
/// </summary>
public class MessagingConfiguration
{
    /// <summary>
    /// Gets or sets the backend key used to resolve a messaging infrastructure factory.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Backward-compatible alias for <see cref="Key"/>.
    /// </summary>
    public string Type
    {
        get => Key;
        set => Key = value;
    }

    /// <summary>
    /// Gets or sets backend-specific configuration values.
    /// </summary>
    public IDictionary<string, string>? Config { get; set; }
}

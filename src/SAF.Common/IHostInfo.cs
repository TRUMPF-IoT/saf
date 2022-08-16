// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

/// <summary>
/// Provides information about the Smart Application Framework (SAF) host instance.
/// </summary>
public interface IHostInfo
{
    /// <summary>
    /// Returns a unique-id for this SAF host instance.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the type of service host (CDE, Test, ...)
    /// </summary>
    string ServiceHostType { get; }

    /// <summary>
    /// Gets the service host startup time.
    /// </summary>
    DateTimeOffset UpSince { get; }

    /// <summary>
    /// Gets the file system base path in which user specific data is stored.
    /// </summary>
    string FileSystemUserBasePath { get; }

    /// <summary>
    /// Gets the file system base path representing the installation folder of the SAF host application.
    /// </summary>
    string FileSystemInstallationPath { get; }
}
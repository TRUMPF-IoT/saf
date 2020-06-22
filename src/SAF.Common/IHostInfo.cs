// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace SAF.Common
{
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
        /// Gets the base path, where the filesystem is accessible to the user.
        /// </summary>
        string FileSystemUserBasePath { get; }

        string FileSystemInstallationPath { get; }
    }
}

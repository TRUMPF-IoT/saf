// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting
{
    public class Configuration
    {
        /// <summary>
        /// Case-insensitive glob based on <see cref="Microsoft.Extensions.FileSystemGlobbing"/> to find SAF service assemblies.
        /// The behavior differs from the normal globbing behavior in the following ways
        /// - multiple patterns can be given using ';' as delimiter
        /// - patterns starting with '|' are considered exclusion patterns
        /// </summary>
        /// <remarks>
        /// The characters ';' and '|' are used, because these are invalid characters for filenames on windows (and bad style for filenames on unix systems).
        /// Exclusion filters can be used to prevent loading of contracts packages.
        /// Example: "MyPrefix.Services.*.dll;|MyPrefix.Services.*Contracts.dll"
        /// </remarks>
        public string SearchPath { get; set; } = default!;

        /// <summary>
        /// Defines the base path to search for SAF service assemblies using the globbing pattern specified in the property <see cref="SearchPath"/>.
        /// </summary>
        /// <remarks>In case no BasePath is specified SAF will use the current AppDomains base directory.</remarks>
        public string BasePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Optional filtering using a RegEx pattern. Matching is performed on filenames without the path (e.g. MyService.dll).
        /// Input are the files found in <see cref="BasePath"/> using the <see cref="SearchPath"/>.
        /// </summary>
        public string SearchFilenamePattern { get; set; } = default!;
    }
}
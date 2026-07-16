// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

/// <summary>
/// Groups the tests that enumerate or mutate files directly under <c>AppContext.BaseDirectory</c>
/// (the plugin-contract assembly scan and the folder-container discovery tests). The collection is
/// marked non-parallel so one test's recursive directory enumeration cannot race with another test
/// creating or copying files/directories in the same base directory.
/// </summary>
[CollectionDefinition("BaseDirectoryFileSystem", DisableParallelization = true)]
public sealed class BaseDirectoryFileSystemCollection;

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Xunit;

namespace SAF.Toolbox.Tests.FileHandling
{
    [CollectionDefinition("FileCollection")]
    public class FileSystemCollection : ICollectionFixture<FileSystemFixture>
    {
    }
}

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using SAF.Toolbox.FileHandling;

namespace SAF.Toolbox.Tests.FileHandling
{
    public class FileSystemFixture : IDisposable
    {
        public const string TestFolderPath = "FileSystemTest";

        private DirectoryInfo BaseDirectory { get; }

        public string TestDirectoryPath => BaseDirectory.FullName;

        public FileSystemFixture()
        {
            BaseDirectory = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), TestFolderPath));
        }

        public IFileSystemDirectory CreateDirectory(bool checkIfExists = true, [CallerMemberName] string methodName = "")
        {
            var path = Path.Combine(TestDirectoryPath, methodName);
            if (Directory.Exists(path) && checkIfExists)
                throw new NotSupportedException($"The directory {path} allready exists - please check the tests to start clean.");
            return new FileSystemDirectory(path);
        }

        public void DeleteDirectory(IFileSystemDirectory directory)
        {
            Directory.Delete(directory.FullPath, true);
        }

        public void Dispose()
        {
            BaseDirectory.Delete(true);
        }
    }
}

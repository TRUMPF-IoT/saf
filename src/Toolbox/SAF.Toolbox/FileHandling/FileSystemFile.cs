// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using System;
using System.Collections.Generic;
using System.IO;

namespace SAF.Toolbox.FileHandling
{
    internal class FileSystemFile : IFileSystemFile
    {
        public FileSystemFile(string absolutePath)
        {
            if (!File.Exists(absolutePath))
                File.Create(absolutePath).Dispose();
            Info = new FileInfo(absolutePath);
        }

        private FileInfo Info { get; }

        public string Name => Info.Name;

        public string FullPath => Info.FullName;

        public bool Exists => File.Exists(FullPath);

        public DateTime CreationTime => Info.CreationTime;

        public DateTime LastWriteTimeUtc => Info.LastWriteTimeUtc;

        public long Length => Info.Length;

        public string ReadText() => File.ReadAllText(FullPath);

        public Stream OpenStream(FileMode fileMode, FileAccess fileAccess) => new FileStream(FullPath, fileMode, fileAccess);

        public string[] ReadLines() => File.ReadAllLines(FullPath);

        public byte[] ReadBytes() => File.ReadAllBytes(FullPath);

        public void Write(byte[] values) => File.WriteAllBytes(FullPath, values);

        public void Write(IEnumerable<string> values) => File.WriteAllLines(FullPath, values);

        public void Write(string value) => File.WriteAllText(FullPath, value);
        public void Delete() => File.Delete(FullPath);
    }
}
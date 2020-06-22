// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SAF.Toolbox.FileHandling
{
    internal class FileSystemDirectory : IFileSystemDirectory
    {
        public const string DefaultSearchPattern = "*.*";
        public const SearchOption DefaultSearchOption = SearchOption.AllDirectories;

        public string FullPath => Info.FullName;

        public string Name => Info.Name;
        public bool Exists => Directory.Exists(FullPath);
        private DirectoryInfo Info { get; }

        public FileSystemDirectory(IHostInfo hostInfo) : this(hostInfo.FileSystemUserBasePath) { }

        internal FileSystemDirectory(string path)
        {
            Info = TryGetDirectoryInfo(path, out var info)
                ? info
                : Directory.CreateDirectory(path);
        }

        public IFileSystemFile CreateFile(string path)
        {
            var absolutePath = GetAbsolutePath(path);
            if (File.Exists(absolutePath)) return new FileSystemFile(absolutePath);

            var directory = GetDirectoryName(path);

            if (!Directory.Exists(directory))
                CreateDirectory(directory);

            return new FileSystemFile(absolutePath);
        }

        public IFileSystemFile GetFile(string path)
        {
            var absolutePath = GetAbsolutePath(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"There was no file at {absolutePath}");

            return new FileSystemFile(absolutePath);
        }

        public IEnumerable<IFileSystemFile> GetFiles()
            => GetFiles(DefaultSearchPattern, DefaultSearchOption);

        public IEnumerable<IFileSystemFile> GetFiles(string searchPattern)
            => GetFiles(searchPattern, DefaultSearchOption);

        public IEnumerable<IFileSystemFile> GetFiles(SearchOption searchOption)
            => GetFiles(DefaultSearchPattern, searchOption);

        public IEnumerable<IFileSystemFile> GetFiles(string searchPattern, SearchOption searchOption)
            => Info.GetFiles(searchPattern.ToLowerInvariant(), searchOption).Select(k => k.FullName).Select(k => new FileSystemFile(k));

        public void Delete()
        {
            if(Exists) Directory.Delete(FullPath, true);
        }

        public IFileSystemDirectory CreateDirectory(string directoryPath)
        {
            var absolutePath = GetAbsolutePath(directoryPath);

            if (!Directory.Exists(absolutePath))
                Directory.CreateDirectory(absolutePath);

            return new FileSystemDirectory(absolutePath);
        }

        public IEnumerable<IFileSystemDirectory> GetDirectories()
            => GetDirectories(DefaultSearchPattern, DefaultSearchOption);

        public IEnumerable<IFileSystemDirectory> GetDirectories(string searchPattern)
            => GetDirectories(searchPattern, DefaultSearchOption);

        public IEnumerable<IFileSystemDirectory> GetDirectories(SearchOption searchOption)
            => GetDirectories(DefaultSearchPattern, searchOption);

        public IEnumerable<IFileSystemDirectory> GetDirectories(string searchPattern, SearchOption searchOption)
            => Info.GetDirectories(searchPattern.ToLowerInvariant(), searchOption).Select(k => k.FullName).Select(k => new FileSystemDirectory(k));

        private bool TryGetDirectoryInfo(string directoryPath, out DirectoryInfo directoryInfo,
            [CallerMemberName] string memberName = "")
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentNullException(
                    $"You can't leave the paramter {nameof(directoryPath)} empty at ${nameof(memberName)}.");

            if (Directory.Exists(directoryPath))
            {
                directoryInfo = new DirectoryInfo(directoryPath);
                return true;
            }

            directoryInfo = null;
            return false;
        }

        private string GetAbsolutePath(string path)
        {
            return Path.IsPathRooted(path)
                ? path : Path.Combine(Info.FullName, path);
        }

        private static string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path);
        }
    }
}

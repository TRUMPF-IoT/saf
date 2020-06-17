// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using System.Collections.Generic;
using System.IO;

namespace SAF.Toolbox.FileHandling
{
    /// <summary>
    /// Provides access to a file system infrastrucure.
    /// </summary>
    public interface IFileSystemDirectory
    {
        /// <summary>
        ///  Absolute path to the base folder.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Name of the base folder.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Determines if the directory exists at the <see cref="FullPath"/>.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Deletes the specified directory and any subdirectories and files in the directory.
        /// </summary>
        void Delete();

        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist.
        /// </summary>
        /// <param name="directoryPath">The directory to create.</param>
        /// <returns>An instance of <see cref="IFileSystemDirectory"/> for the created directory.</returns>
        IFileSystemDirectory CreateDirectory(string directoryPath);

        /// <summary>
        /// Returns an array of all sub directories.
        /// </summary>
        /// <returns>An array of <see cref="IFileSystemDirectory"/>.</returns>
        IEnumerable<IFileSystemDirectory> GetDirectories();

        /// <summary>
        /// Returns an array of all sub directories.
        /// </summary>
        /// <param name="searchPattern">The search string to match against the names of directories.  This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions. The default pattern is "*", which returns all files.</param>
        /// <returns>An array of <see cref="IFileSystemDirectory"/>.</returns>
        IEnumerable<IFileSystemDirectory> GetDirectories(string searchPattern);

        /// <summary>
        /// Returns an array of all sub directories.
        /// </summary>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or all subdirectories.</param>
        /// <returns>An array of <see cref="IFileSystemDirectory"/>.</returns>
        IEnumerable<IFileSystemDirectory> GetDirectories(SearchOption searchOption);

        /// <summary>
        /// Returns an array of all sub directories.
        /// </summary>
        /// <param name="searchPattern">The search string to match against the names of directories.  This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions. The default pattern is "*", which returns all files.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or all subdirectories.</param>
        /// <returns>An array of <see cref="IFileSystemDirectory"/>.</returns>
        IEnumerable<IFileSystemDirectory> GetDirectories(string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Returns a file list of <see cref="IFileSystemFile"/> matching the given search pattern and using a value to determine whether to search subdirectories.
        /// </summary>
        IEnumerable<IFileSystemFile> GetFiles();

        /// <summary>
        /// Returns a file list of file <see cref="IFileSystemFile"/> matching the given search pattern and using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="searchPattern">The search string to match against the names of files.  This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions. The default pattern is "*", which returns all files.</param>
        /// <returns>An array of <see cref="IFileSystemFile"/> matching <paramref name="searchPattern">searchPattern</paramref>.</returns>
        IEnumerable<IFileSystemFile> GetFiles(string searchPattern);

        /// <summary>
        /// Returns a file list of file paths matching the given search pattern and using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or all subdirectories.</param>
        /// <returns>An array of <see cref="IFileSystemFile"/> matching the <see cref="SearchOption"/>.</returns>
        IEnumerable<IFileSystemFile> GetFiles(SearchOption searchOption);

        /// <summary>
        /// Returns a file list of file paths matching the given search pattern and using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="searchPattern">The search string to match against the names of files.  This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions. The default pattern is "*", which returns all files.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or all subdirectories.</param>
        /// <returns>An array of <see cref="IFileSystemFile"/> matching <paramref name="searchPattern">searchPattern</paramref>.</returns>
        IEnumerable<IFileSystemFile> GetFiles(string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Creates a file at the given <see cref="path"/>
        /// </summary>
        /// <param name="path">path of the new file</param>
        /// <returns>Returns a new <see cref="IFileSystemFile"/> for the given <see cref="path"/>.</returns>
        IFileSystemFile CreateFile(string path);

        /// <summary>
        /// Gets a file at the given <see cref="path"/>
        /// </summary>
        /// <param name="path">path of the existing file.</param>
        /// <returns>Returns the existing <see cref="IFileSystemFile"/> for the given <see cref="path"/>.</returns>
        IFileSystemFile GetFile(string path);
    }
}

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Collections.Generic;
using System.IO;

namespace SAF.Toolbox.FileHandling
{

    public interface IFileSystemFile
    {
        /// <summary>
        ///  Absolute path to the current file.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Name of the current file.
        /// </summary>
        string Name { get; }

        /// <summary>The creation date and time of the current file.</summary>
        DateTime CreationTime { get; }

        /// <summary>Gets the time, in coordinated universal time (UTC), when the current file or directory was last written to.</summary>
        DateTime LastWriteTimeUtc { get; }

        /// <summary>
        /// Determines if the file exists at the <see cref="FullPath"/>.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Gets the size, in bytes, of the current file.
        /// </summary>
        long Length { get; }

        /// <summary>Opens a binary file, reads the contents of the file into a byte array, and then closes the file.</summary>
        /// <returns>A byte array containing the contents of the file.</returns>
        byte[] ReadBytes();

        /// <summary>Opens a text file, reads all lines of the file, and then closes the file.</summary>
        /// <returns>A string array containing all lines of the file.</returns>
        string[] ReadLines();

        /// <summary>Opens a text file, reads all lines of the file, and then closes the file.</summary>
        /// <returns>A string containing all lines of the file.</returns>
        string ReadText();

        /// <summary>
        /// Provides a Stream for a file, supporting both synchronous and asynchronous read and write operations.
        /// </summary>
        /// <returns></returns>
        Stream OpenStream(FileMode fileMode, FileAccess fileAccess);

        /// <summary>
        ///     Creates a new file, writes the specified byte array to the file, and then closes the file. If the target file
        ///     already exists, it is overwritten.
        /// </summary>
        /// <param name="values">The bytes to write to the file.</param>
        void Write(byte[] values);

        /// <summary>Creates a new file, writes a collection of strings to the file, and then closes the file.</summary>
        /// <param name="values">The lines to write to the file.</param>
        void Write(IEnumerable<string> values);

        /// <summary>
        ///     Creates a new file, writes the specified string to the file, and then closes the file. If the target file
        ///     already exists, it is overwritten.
        /// </summary>
        /// <param name="value">The string to write to the file.</param>
        void Write(string value);

        /// <summary>
        /// Deletes the file
        /// </summary>
        void Delete();
    }
}
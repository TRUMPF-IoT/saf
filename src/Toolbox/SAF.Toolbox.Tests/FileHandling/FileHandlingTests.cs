// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAF.Toolbox.FileHandling;
using Xunit;

namespace SAF.Toolbox.Tests.FileHandling
{
    [Collection("FileCollection")]
    public class FileSystemTests
    {
        public FileSystemFixture Fixture { get; }


        public FileSystemTests(FileSystemFixture fixture) => Fixture = fixture;

        [Fact]
        public void TestInitialCreate()
        {

            var directory = Fixture.CreateDirectory();
            Assert.True(Directory.Exists(directory.FullPath));
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(directory.FullPath));
        }

        [Fact]
        public void TestInitialCreateButFolderExists()
        {
            var path = Path.Combine(Fixture.TestDirectoryPath, nameof(TestInitialCreateButFolderExists));
            Directory.CreateDirectory(path);
            Assert.True(Directory.Exists(path));
            var directory = new FileSystemDirectory(path);
            Assert.True(Directory.Exists(path));
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(path));
        }

        [Fact]
        public void TestCreateDirectoryRelativePath()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(Directory.Exists(directory.FullPath));
            // --- Test Start --- //

            var relativeDirectory = directory.CreateDirectory(@"..\RelativeTestFolder");

            Assert.True(relativeDirectory.Exists);

            Fixture.DeleteDirectory(relativeDirectory);
            Assert.False(relativeDirectory.Exists);
            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(directory.FullPath));
        }

        [Fact]
        public void TestCreateDirectoryAbsolutePath()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(Directory.Exists(directory.FullPath));
            // --- Test Start --- //

            directory.CreateDirectory(Path.Combine(Fixture.TestDirectoryPath, nameof(TestCreateDirectoryAbsolutePath), "MyFolder", "AnotherFolder"));
            var folderToSearch = Path.Combine(Fixture.TestDirectoryPath, nameof(TestCreateDirectoryAbsolutePath), "MyFolder", "AnotherFolder");
            Assert.True(Directory.Exists(folderToSearch));

            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(directory.FullPath));
            Assert.False(Directory.Exists(folderToSearch));
        }

        [Fact]
        public void TestGetAllSubFoldersAbsolut()
        {

            var path = Path.Combine(Fixture.TestDirectoryPath, nameof(TestGetAllSubFoldersAbsolut));
            Directory.CreateDirectory(path);

            var subFolders = new List<string>
            {
                Path.Combine(path, "MySubFolder1"),
                Path.Combine(path, "MySubFolder2"),
                Path.Combine(path, "MySubFolder3"),
                Path.Combine(path, "MySubFolder1","MySubFolder1"),
                Path.Combine(path, "MySubFolder2","MySubFolder2"),
                Path.Combine(path, "MySubFolder3","MySubFolder3"),
                Path.Combine(path, "MySubFolder1","MySubFolder1","MySubFolder1"),
                Path.Combine(path, "MySubFolder2","MySubFolder2","MySubFolder2"),
                Path.Combine(path, "MySubFolder3","MySubFolder3","MySubFolder3")
            };

            foreach (var folder in subFolders)
            {
                Directory.CreateDirectory(folder);
                Directory.Exists(folder);
            }

            Assert.True(Directory.Exists(path));
            var directory = Fixture.CreateDirectory(false);
            var folderPaths = directory.GetDirectories().Select(k => k.FullPath).ToList();

            foreach (var folder in folderPaths)
            {
                Assert.Contains(folder, subFolders);
            }

            Assert.Equal(subFolders.Count, folderPaths.Count);


            Assert.True(Directory.Exists(path));
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(path));


            foreach (var folder in folderPaths)
            {
                Assert.False(Directory.Exists(folder));
            }


        }

        [Fact]
        public void TestGetAllSubFoldersRelative()
        {

            var path = Path.Combine(Fixture.TestDirectoryPath, nameof(TestGetAllSubFoldersRelative));
            Directory.CreateDirectory(path);
            Assert.True(Directory.Exists(path));
            var directory = Fixture.CreateDirectory(false);

            var subFolders = new List<string>
            {
                Path.Combine("MySubFolder1"),
                Path.Combine("MySubFolder2"),
                Path.Combine("MySubFolder3"),
                Path.Combine("MySubFolder1","MySubSubFolder1"),
                Path.Combine("MySubFolder2","MySubSubFolder2"),
                Path.Combine("MySubFolder3","MySubSubFolder3"),
                Path.Combine("MySubFolder1","MySubSubFolder1","MySubSubSubFolder1"),
                Path.Combine("MySubFolder2","MySubSubFolder2","MySubSubSubFolder2"),
                Path.Combine("MySubFolder3","MySubSubFolder3","MySubSubSubFolder3")
            };

            var subAbsoluteFolders = new List<string>
            {
                Path.Combine(path, "MySubFolder1"),
                Path.Combine(path, "MySubFolder2"),
                Path.Combine(path, "MySubFolder3"),
                Path.Combine(path, "MySubFolder1","MySubSubFolder1"),
                Path.Combine(path, "MySubFolder2","MySubSubFolder2"),
                Path.Combine(path, "MySubFolder3","MySubSubFolder3"),
                Path.Combine(path, "MySubFolder1","MySubSubFolder1","MySubSubSubFolder1"),
                Path.Combine(path, "MySubFolder2","MySubSubFolder2","MySubSubSubFolder2"),
                Path.Combine(path, "MySubFolder3","MySubSubFolder3","MySubSubSubFolder3")
            };


            Assert.Equal(subAbsoluteFolders.Count, subFolders.Count);

            foreach (var folder in subFolders)
            {
                directory.CreateDirectory(folder);
                Directory.Exists(folder);
            }


            var folderPaths = directory.GetDirectories().Select(k => k.FullPath).ToList();

     

            foreach (var folder in folderPaths)
            {
                Assert.Contains(folder, subAbsoluteFolders);
            }

            Assert.Equal(subFolders.Count, folderPaths.Count);


            Assert.True(Directory.Exists(path));
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(path));

            foreach (var folder in folderPaths)
            {
                Assert.False(Directory.Exists(folder));
            }
        }


        [Fact]
        public void TestWriteFileAbsolute()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(Directory.Exists(directory.FullPath));
            // --- Test Start --- //

            directory.CreateFile("MyTextFile.txt").Write(nameof(TestWriteFileAbsolute));
            var createdFile = directory.GetFiles().First();
            var text = createdFile.ReadText();
            Assert.Equal(nameof(TestWriteFileAbsolute), text);
            createdFile.Delete();
            Assert.False(createdFile.Exists);
            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
            Assert.False(directory.Exists);
        }


        [Fact]
        public void TestWriteFileRelative()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //

            var file = directory.CreateFile("MyTextFile.txt");
            Assert.True(file.Exists);
            file.Write(nameof(TestWriteFileRelative));
            var createdFile = directory.GetFiles().First();
            Assert.Equal(file.FullPath, createdFile.FullPath);
            var text = file.ReadText();
            Assert.Equal(nameof(TestWriteFileRelative), text);
            file.Delete();
            Assert.False(file.Exists);
            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
        }


        [Fact]
        public void TestWriteEmptyFile()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //

            var file = directory.CreateFile("MyTextFile.txt");
            Assert.True(file.Exists);
            file.Delete();
            File.Exists(file.FullPath);
            Assert.False(file.Exists);
            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
            Assert.False(directory.Exists);
        }

        [Fact]
        public void TestWriteEmptyLinesFile()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //

            var file = directory.CreateFile("MyTextFile.txt");
            file.Write(new List<string>());
            var createdFile = directory.GetFiles().First();
            var text = createdFile.ReadText();
            Assert.Equal(string.Empty, text);
            // --- Test End --- //
            Fixture.DeleteDirectory(directory);
            Assert.False(Directory.Exists(directory.FullPath));
        }

        [Fact]
        public void TestReadFiles()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //
            var filePath = Path.Combine(directory.FullPath, "MyTextFile.txt");

            File.WriteAllText(filePath, nameof(TestReadFiles));
            File.Exists(filePath);


            var files = directory.GetFiles().ToList();

            Assert.True(files.Any());
            Assert.Single(files);
            Assert.Equal(files.First().FullPath, filePath);
            files.First().Delete();
            Assert.False(files.First().Exists);

            Fixture.DeleteDirectory(directory);
            // --- Test End --- //
            Assert.False(directory.Exists);
        }

        [Fact]
        public void TestGetFileAbsolutePath()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //
            var filePath = Path.Combine(directory.FullPath, "MyTextFile.txt");

            File.WriteAllText(filePath, nameof(TestGetFileAbsolutePath));
            File.Exists(filePath);


            var file = directory.GetFile(filePath);
            Assert.True(file.Exists);
            
            Fixture.DeleteDirectory(directory);
            // --- Test End --- //
            Assert.False(directory.Exists);
        }

        [Fact]
        public void TestGetFileRelativePath()
        {
            var directory = Fixture.CreateDirectory();
            Assert.True(directory.Exists);
            // --- Test Start --- //
            var filePath = Path.Combine(directory.FullPath, "MyTextFile.txt");

            File.WriteAllText(filePath, nameof(TestGetFileRelativePath));
            File.Exists(filePath);


            var file = directory.GetFile("MyTextFile.txt");
            Assert.True(file.Exists);

            Fixture.DeleteDirectory(directory);
            // --- Test End --- //
            Assert.False(directory.Exists);
        }
    }
}

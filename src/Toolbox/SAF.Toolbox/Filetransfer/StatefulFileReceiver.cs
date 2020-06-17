// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SAF.Toolbox.Filetransfer
{
	public class StatefulFileReceiver : IStatefulFileReceiver
    {
        private readonly ILogger<StatefulFileReceiver> _log;

        public StatefulFileReceiver(ILogger<StatefulFileReceiver> log)
        {
            _log = log ?? NullLogger<StatefulFileReceiver>.Instance;
        }

        public event Action<string> FileReceived;
        public event Action<string> StreamReceived;

        /// <summary>
        /// Saves a TransportFileDelivery to disk.
        /// </summary>
        /// <param name="folderPath">The path to save the file to.</param>
        /// <param name="delivery">The delivery package.</param>
        /// <param name="overwrite">Whether to overwrite the existing file or not.</param>
        public void WriteFile(string folderPath, TransportFileDelivery delivery, bool overwrite)
        {
            var tf = delivery.TransportFile;

            if(!tf.Verify()) return; // Do not write corrupted files

            var directory = !Directory.Exists(folderPath)
                ? Directory.CreateDirectory(folderPath)
                : new DirectoryInfo(folderPath);

            var props = tf.Properties.FromDictionary();

            if (props.IsChunked)
                WriteChunkedFile(directory, delivery, props, overwrite);
            else if (props.IsFileStream)
                WriteFileStream(directory, delivery, overwrite);
            else
                WriteFullFile(directory, delivery, overwrite);
        }

        /// <summary>
        /// Saves a TransportFileDelivery to disk.
        /// Saving Mechanism is inconsistent. Writing full file or a fileStream overwrites/appends the file,
        /// while chunks create a new version of the file. 
        /// </summary>
        /// <param name="folderPath">The path to save the file to.</param>
        /// <param name="delivery">The delivery package.</param>
        [Obsolete("WriteFile(string, TransportFileDelivery) is deprecated, please use WriteFile(string, TransportFileDelivery, bool) instead.")]
        public void WriteFile(string folderPath, TransportFileDelivery delivery)
        {
            var tf = delivery.TransportFile;

            if(!tf.Verify()) return; // Do not write corrupted files

            var directory = !Directory.Exists(folderPath)
                ? Directory.CreateDirectory(folderPath)
                : new DirectoryInfo(folderPath);

            var props = tf.Properties.FromDictionary();

            if (props.IsChunked)
                WriteChunkedFile(directory, delivery, props, false);
            else if (props.IsFileStream)
                WriteFileStream(directory, delivery, false);
            else
                WriteFullFile(directory, delivery, true);
        }

        private static void DeleteExistingFile(TransportFileDelivery delivery, FileSystemInfo directory)
        {
            var fileName = delivery.TransportFile.Name;
            var filePath = Path.Combine(directory.FullName, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        private void WriteChunkedFile(FileSystemInfo directory, TransportFileDelivery delivery, FileTransferProperties props, bool overwrite)
        {
            var fileName = delivery.TransportFile.Name;
            var filePath = GenerateUniqueFileName(Path.Combine(directory.FullName, fileName), overwrite);

            var uniqueTransferId = props.TransferId ?? 0;
            var tmpName = Path.ChangeExtension(filePath, $".{uniqueTransferId}.temp");

            _log.LogDebug($"Processing chunk no. {props.ChunkNumber} - is last {props.LastChunk}");

            if(filePath == null) return; // only to make static code analysis happy.


            try
            {
                long maxChunks;
                if (props.LastChunk) maxChunks = props.ChunkNumber + 1;
                else
                {
                    maxChunks = props.FileSize / props.ChunkSize;
                    maxChunks += props.FileSize % props.ChunkSize != 0 ? 1 : 0;
                }
                var headerSizeInBytes = ChunkedFileHeader.GetHeaderLengthInBytes(maxChunks);

                // Wait here to write to file
                var log = _log; // needed to make static code analysis happy (avoid using variable in and outside synchronization blocks warning)
                lock (string.Intern(tmpName))
                {
                    var tmpFileSize = headerSizeInBytes + props.FileSize;
                    CheckTempFileConsistency(tmpName, uniqueTransferId, tmpFileSize);

                    using (var fs = new FileStream(tmpName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        using (var mmf = MemoryMappedFile.CreateFromFile(fs, null, tmpFileSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.Inheritable, false))
                        {
                            var fileHeader = ChunkedFileHeader.ReadFromMemoryMappedFile(mmf, maxChunks);
                            using (var accessor = mmf.CreateViewStream(headerSizeInBytes + props.ChunkOffset, 0))
                            {
                                delivery.TransportFile.WriteTo(accessor);
                            }

                            fileHeader.ChunksReceived[props.ChunkNumber] = true;
                            fileHeader.WriteToMemoryMappedFile(mmf);

                            var isFileComplete = fileHeader.ChunksReceived.All(b => b);
                            if (!isFileComplete) return;

                            try
                            {
                                if (overwrite)
                                    DeleteExistingFile(delivery, directory);

                                SaveCompletedFileContent(mmf, filePath, headerSizeInBytes, props.FileSize);
                                log.LogInformation($"File written to {filePath}");
                            }
                            catch (IOException e)
                            {
                                log.LogError($"Could not move file from {tmpName} to {filePath} because of {e.Message}");
                                throw;
                            }
                        }
                    }

                    File.Delete(tmpName);
                }

                FileReceived?.Invoke(filePath);
            }
            catch(IOException e)
            {
                _log.LogError($"Could not handle file {tmpName} -> {filePath}", e);
            }
        }

        private void CheckTempFileConsistency(string tmpName, long uniqueTransferId, long fileSize)
        {
            if (!File.Exists(tmpName)) return;

            var fi = new FileInfo(tmpName);
            if (fi.Length != fileSize)
            {
                File.Delete(tmpName);
                return;
            }

            var filesTransferIdString = Path.GetExtension(Path.GetFileNameWithoutExtension(tmpName)).Trim('.');
            if (string.IsNullOrEmpty(filesTransferIdString))
            {
                if(uniqueTransferId != 0) File.Delete(tmpName);
                return;
            }

            var filesTransferId = Convert.ToInt64(filesTransferIdString);
            if (filesTransferId != uniqueTransferId)
            {
                File.Delete(tmpName);
            }
        }

        private void SaveCompletedFileContent(MemoryMappedFile mmf, string filePath, long headerSizeInBytes, long fileSize)
        {
            using (var targetFile = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var accessor = mmf.CreateViewStream(headerSizeInBytes, fileSize))
                {
                    accessor.CopyTo(targetFile);
                }
            }
        }

        private static string GenerateUniqueFileName(string filePath, bool overwrite)
        {
            var directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrEmpty(directory)) return filePath;

            var fileName = Path.GetFileName(filePath);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var n = 0;
            while(File.Exists(filePath) && !overwrite)
            {
                var pattern = $"{name}({++n}){ext}";
                filePath = Path.Combine(directory, pattern);
            }

            return filePath;
        }

        private void WriteFileStream(FileSystemInfo directory, TransportFileDelivery delivery, bool overwrite)
        {
            var fileName = delivery.TransportFile.Name;
            var filePath = Path.Combine(directory.FullName, fileName);

            if (overwrite)
                DeleteExistingFile(delivery, directory);

            using(var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                delivery.TransportFile.WriteTo(fs);

            StreamReceived?.Invoke(filePath);
        }

        private void WriteFullFile(FileSystemInfo directory, TransportFileDelivery delivery, bool overwrite)
        {
            var fileName = delivery.TransportFile.Name;
            var filePath = Path.Combine(directory.FullName, fileName);

            if(overwrite)
                DeleteExistingFile(delivery, directory);

            using (var fs = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                delivery.TransportFile.WriteTo(fs);

            FileReceived?.Invoke(filePath);
        }
    }
}
// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.IO.MemoryMappedFiles;

namespace SAF.Toolbox.FileTransfer
{
    internal struct ChunkedFileHeader
    {
        public int Version;
        public long MaxChunks;
        public bool[] ChunksReceived;

        public static ChunkedFileHeader ReadFromMemoryMappedFile(MemoryMappedFile mmf, long maxChunks)
        {
            var header = new ChunkedFileHeader();
            using (var headerAccessor = mmf.CreateViewStream(0, GetHeaderLengthInBytes(maxChunks)))
            {
                var buffer = new byte[sizeof(int)];
                headerAccessor.Read(buffer, 0, buffer.Length);
                header.Version = BitConverter.ToInt32(buffer, 0);

                buffer = new byte[sizeof(long)];
                headerAccessor.Read(buffer, 0, buffer.Length);
                header.MaxChunks = BitConverter.ToInt64(buffer, 0);
                if (header.MaxChunks == 0) header.MaxChunks = maxChunks;

                var chunksReceivedBytes = header.MaxChunks * sizeof(bool);
                buffer = new byte[chunksReceivedBytes];
                headerAccessor.Read(buffer, 0, buffer.Length);

                header.ChunksReceived = new bool[chunksReceivedBytes / sizeof(bool)];
                Buffer.BlockCopy(buffer, 0, header.ChunksReceived, 0, header.ChunksReceived.Length);
            }
            return header;
        }

        public void WriteToMemoryMappedFile(MemoryMappedFile mmf)
        {
            using (var headerAccessor = mmf.CreateViewStream(0, LengthInBytes))
            {
                var buffer = BitConverter.GetBytes(Version);
                headerAccessor.Write(buffer, 0, buffer.Length);

                buffer = BitConverter.GetBytes(MaxChunks);
                headerAccessor.Write(buffer, 0, buffer.Length);

                buffer = new byte[ChunksReceived.Length * sizeof(bool)];
                Buffer.BlockCopy(ChunksReceived, 0, buffer, 0, buffer.Length);
                headerAccessor.Write(buffer, 0, buffer.Length);
            }
        }

        private long LengthInBytes => GetHeaderLengthInBytes(MaxChunks);
        public static long GetHeaderLengthInBytes(long maxChunks) => sizeof(int) + sizeof(long) + maxChunks * sizeof(bool);
    }
}
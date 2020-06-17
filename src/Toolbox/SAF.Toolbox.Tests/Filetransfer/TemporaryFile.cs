// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.IO;

namespace SAF.Toolbox.Tests.FileTransfer
{
    internal class TemporaryFile : IDisposable
    {
        public string TempFilePath { get; }

        public TemporaryFile(string fileName, byte[] buffer)
        {
            var curDir = Directory.GetCurrentDirectory();
            TempFilePath = Path.Combine(curDir, fileName);
            Write(buffer);
        }

        private void Write(byte[] buffer)
        {
            File.WriteAllBytes(TempFilePath, buffer);
        }

        public void Dispose()
        {
            if(File.Exists(TempFilePath))
                File.Delete(TempFilePath);
        }
    }
}

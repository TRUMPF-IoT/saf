// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities
{
    public class TemporaryFileCopy : IDisposable
    {
        public string TempFileName { get; }
        public string TempFilePath { get; }

        public TemporaryFileCopy(string originalFileRelativePath)
        {
            var curDir = Directory.GetCurrentDirectory();
            var originalFilePath = Path.Combine(curDir, originalFileRelativePath);
            TempFileName = $"{Path.GetFileNameWithoutExtension(originalFilePath)}_{Guid.NewGuid()}_.{Path.GetExtension(originalFilePath)}";
            TempFilePath = Path.Combine(curDir, TempFileName);
            File.Copy(originalFilePath, TempFilePath);
        }

        public void Dispose()
        {
            File.Delete(TempFilePath);
        }
    }
}
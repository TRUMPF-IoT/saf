// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.FileTransfer;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer
{
    public class TransportFileTests
    {
        [Theory]
        [InlineData(8)] // not dividable by 3 (+2)
        [InlineData(16)] // not dividable by 3 (+1)
        [InlineData(9)] // dividable by 3
        [InlineData(0)] // empty
        [InlineData(81920)] // exactly one buffer - not dividable by 3
        [InlineData(81921)] // more then one buffer - dividable by 3
        [InlineData(81925)] // more then one buffer - not dividable by 3
        public void EncodeDecodeLengthsMatchForDifferentLengths(int numberOfBytes)
        {
            var fileName = "Test";
            var mimeType = "application/zip";

            var transFile = new TransportFile(fileName, mimeType);
            var srcData = new byte[numberOfBytes];

            var rand = new Random();
            rand.NextBytes(srcData);

            using (var srcMs = new MemoryStream(srcData))
                transFile.ReadFrom(srcMs);

            byte[] destData;
            using (var destMs = new MemoryStream())
            {
                transFile.WriteTo(destMs);
                destData = destMs.ToArray();
            }

            Assert.Equal(srcData.Length, destData.Length);
            Assert.Equal(srcData, destData);
        }

        [Fact]
        public void TransportFileWithoutKnownLengthShouldBeDecodable()
        {
            var fileName = "Test";
            var mimeType = "application/zip";

            var transFile = new TransportFile(fileName, mimeType);
            var srcData = new byte[81924]; // has to be dividable by 3 without known length
            var rand = new Random();
            rand.NextBytes(srcData);

            using (var srcMs = new MemoryStream(srcData))
                transFile.ReadFrom(srcMs);

            transFile.OriginalLength = 0; // Reset to default state!

            byte[] destData;
            using (var destMs = new MemoryStream())
            {
                transFile.WriteTo(destMs);
                destData = destMs.ToArray();
            }

            Assert.Equal(srcData.Length, destData.Length);
            Assert.Equal(srcData, destData);
        }
    }
}

// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Data.SQLite;
using System.Threading.Tasks;
using Xunit;

namespace SAF.Storage.SqLite.Test
{
    public class StorageTests
    {
        private readonly SQLiteConnection _connection;

        public StorageTests()
        {
            string cs = "Data Source=:memory:";
            _connection = new SQLiteConnection(cs);
        }

        [Fact]
        public void NotExistingKeysReturnNullOk()
        {
            using var storage = new Storage(_connection);

            Assert.Null(storage.GetBytes("test"));
            Assert.Null(storage.GetString("test"));
        }

        [Fact]
        public void SettingByteValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualByteArray = new byte[] { 0, 1, 2, 3 };
            storage.Set("test", actualByteArray);
            Assert.Equal(actualByteArray, storage.GetBytes("test"));
        }

        [Fact]
        public void OverwriteByteValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualByteArray = new byte[] { 0, 1, 2, 3 };
            storage.Set("test", actualByteArray);
            var overwrittenByteArray = new byte[] { 1, 2, 3, 4 };
            storage.Set("test", overwrittenByteArray);
            Assert.Equal(overwrittenByteArray, storage.GetBytes("test"));
        }

        [Fact]
        public void SettingByteAreaValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualByteArray = new byte[] { 0, 1, 2, 3 };
            storage.Set("area", "test", actualByteArray);
            Assert.Equal(actualByteArray, storage.GetBytes("area", "test"));
        }


        [Fact]
        public void SettingStringValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualStringValue = "42";
            storage.Set("test", actualStringValue);
            Assert.Equal(actualStringValue, storage.GetString("test"));
        }

        [Fact]
        public void OverwriteStringValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualStringValue = "42";
            storage.Set("test", actualStringValue);
            var overwrittenStringValue = "43";
            storage.Set("test", overwrittenStringValue);
            Assert.Equal(overwrittenStringValue, storage.GetString("test"));
        }

        [Fact]
        public void SettingStringAreaValuesOk()
        {
            using var storage = new Storage(_connection);

            var actualStringValue = "42";
            storage.Set("area", "test", actualStringValue);
            Assert.Equal(actualStringValue, storage.GetString("area", "test"));
        }

        [Fact]
        public void LegacyCallsOk()
        {
            using var storage = new Storage(_connection);

            var uniqueArea = "area.area1/area3";
            var key = "key1.key2/key3";
            var expectedValue = "my starnge value";
            storage.Set(uniqueArea, key, expectedValue);

            Assert.Equal(expectedValue, storage.GetString(uniqueArea, key));
        }

        [Fact]
        public void GetStringEntryAsByteNotOk()
        {
            using var storage = new Storage(_connection);

            var area = "areaName";
            var key = "globalKey";
            var expectedValue = "value";
            storage.Set(area, key, expectedValue);

            Assert.Equal(expectedValue, storage.GetString(area, key));
            Assert.Null(storage.GetBytes(area, key));
        }

        [Fact]
        public void ParallelCallsOk()
        {
            using var storage = new Storage(_connection);

            Parallel.For(0, 20, id =>
            {
                var storageId = $"Storage{id}";
                Parallel.For(0, 10, fId =>
                {
                    var stringId = fId.ToString();
                    storage.Set(storageId, stringId, stringId);
                });

                Parallel.For(0, 10, fId =>
                {
                    var stringId = fId.ToString();
                    var result = storage.GetString(storageId, stringId);
                    Assert.Equal(stringId, result);
                });
            });
        }

    }
}

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

        [Fact]
        public void RemoveKeyOk()
        {
            using var storage = new Storage(_connection);

            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(keyToDelete, new byte[] { 9, 9, 9 });
            storage.Set(keyToStay, nameof(keyToStay));

            storage.RemoveKey(keyToDelete);

            Assert.Null(storage.GetBytes(keyToDelete));
            Assert.Equal(nameof(keyToStay), storage.GetString(keyToStay));
        }

        [Fact]
        public void RemoveKeyFromAreaOk()
        {
            using var storage = new Storage(_connection);

            const string areaToDeleteFrom = "areaToDeleteFrom";
            const string areaToNotTouch = "areaToNotTouch";
            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(areaToDeleteFrom, keyToDelete, new byte[] { 9, 9, 9 });
            storage.Set(areaToDeleteFrom, keyToStay, nameof(keyToStay));

            storage.Set(areaToNotTouch, keyToStay, nameof(keyToStay));

            storage.RemoveKey(areaToDeleteFrom, keyToDelete);

            Assert.Null(storage.GetBytes(areaToDeleteFrom, keyToDelete));
            Assert.Equal(nameof(keyToStay), storage.GetString(areaToDeleteFrom, keyToStay));
            Assert.Equal(nameof(keyToStay), storage.GetString(areaToNotTouch, keyToStay));
        }

        [Fact]
        public void RemoveAreaOk()
        {
            using var storage = new Storage(_connection);

            const string areaToDelete = "areaToDelete";
            const string areaToStay = "areaToStay";
            const string keyToStay = "keyToStay";

            storage.Set(areaToDelete, "bytesKeyToDelete", new byte[] { 9, 9, 9 });
            storage.Set(areaToDelete, "stringKeyToDelete", nameof(keyToStay));

            storage.Set(areaToStay, keyToStay, nameof(keyToStay));

            storage.RemoveArea(areaToDelete);

            Assert.Null(storage.GetBytes(areaToDelete, "bytesKeyToDelete"));
            Assert.Null(storage.GetString(areaToDelete, "stringKeyToDelete"));

            Assert.Equal(nameof(keyToStay), storage.GetString(areaToStay, keyToStay));
        }

        [Fact]
        public void RemoveUnknownKeyOk()
        {
            using var storage = new Storage(_connection);

            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(keyToStay, nameof(keyToStay));

            storage.RemoveKey(keyToDelete);

            Assert.Equal(nameof(keyToStay), storage.GetString(keyToStay));
        }

        [Fact]
        public void RemoveUnknownAreaOk()
        {
            using var storage = new Storage(_connection);

            const string keyToStay = "keyToStay";
            const string testArea = nameof(testArea);
            storage.Set(testArea, keyToStay, nameof(keyToStay));

            storage.RemoveArea("areaDoesNotExist");

            Assert.Equal(nameof(keyToStay), storage.GetString(testArea, keyToStay));
        }
    }
}

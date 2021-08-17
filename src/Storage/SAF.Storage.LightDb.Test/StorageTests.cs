// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Xunit;
using LiteDB;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SAF.Storage.LiteDb.Test
{
    public class StorageTests : IDisposable
    {
        private string DBName = "Filename=LocalLightDb.db;Connection=direct";

        public StorageTests()
        {
            if(File.Exists("LocalLightDb.db"))
                File.Delete("LocalLightDb.db");
        }

        [Fact]
        public void WriteStringOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var key = "globalKey";
                var expectedValue = "value";
                storage.Set(key, expectedValue);

                var col = db.GetCollection<StringEntry>("global");
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
                Assert.Equal(expectedValue, storage.GetString(key));
            }            
        }

        [Fact]
        public void WriteStringAreaOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var area = "areaName";
                var key = "globalKey";
                var expectedValue = "value";
                storage.Set(area, key, expectedValue);

                var col = db.GetCollection<StringEntry>(area);
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
                Assert.Equal(expectedValue, storage.GetString(area, key));
            }
        }


        [Fact]
        public void GetStringEntryAsByteNotOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {

                var storage = new Storage(db);

                var area = "areaName";
                var key = "globalKey";
                var expectedValue = "value";
                storage.Set(area, key, expectedValue);

                Assert.Equal(expectedValue, storage.GetString(area, key));
                Assert.Null(storage.GetBytes(area, key));
            }
        }

        [Fact]
        public void WriteByteOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var key = "globalKey";
                var expectedValue = new byte[] { 1, 2, 3, 4, 5, 6 };
                storage.Set(key, expectedValue);

                var col = db.GetCollection<ByteEntry>("global");
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
                Assert.Equal(expectedValue, storage.GetBytes(key));
            }
        }

        [Fact]
        public void WriteByteAreaOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {

                var storage = new Storage(db);

                var area = "areaName";
                var key = "globalKey";
                var expectedValue = new byte[] { 1, 2, 3, 4, 5, 6 };
                storage.Set(area, key, expectedValue);

                var col = db.GetCollection<ByteEntry>(area);
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
                Assert.Equal(expectedValue, storage.GetBytes(area, key));
            }
        }

        [Fact]
        public void OverwriteStringOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var key = "globalKey";
                var expectedValue = "value";
                storage.Set(key, "value2");
                storage.Set(key, expectedValue);

                var col = db.GetCollection<StringEntry>("global");
                Assert.Equal(1, col.Count());
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
            }
        }


        [Fact]
        public void OverwriteByteOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var key = "globalKey";
                var expectedValue = new byte[] { 1, 2, 3, 4, 5, 6 };
                storage.Set(key, new byte[] { 9, 9, 9 });
                storage.Set(key, expectedValue);

                var col = db.GetCollection<ByteEntry>("global");
                Assert.Equal(1, col.Count());
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
            }
        }

        [Fact]
        public void ParallelCallsOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Direct
            }))
            {
                var storage = new Storage(db);

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

        [Fact]
        public void LegacyCallsOk()
        {
            using (var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            }))
            {
                var storage = new Storage(db);

                var uniqueArea = "area.area1/area3";
                var key = "key1.key2/key3";
                var expectedValue = "my starnge value";
                storage.Set(uniqueArea, key, expectedValue);

                var col = db.GetCollection<StringEntry>("area_area1_area3");
                Assert.Equal(1, col.Count());
                var savedValue = col.FindOne(d => d.Key == key);
                Assert.Equal(expectedValue, savedValue.Value);
                Assert.Equal(expectedValue, storage.GetString(uniqueArea, key));
            }
        }

        [Fact]
        public void RemoveKeyOk()
        {
            using var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            });
            var storage = new Storage(db);

            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(keyToDelete, new byte[] { 9, 9, 9 });
            storage.Set(keyToStay, nameof(keyToStay));

            var col = db.GetCollection<ByteEntry>("global");
            Assert.Equal(2, col.Count());

            storage.RemoveKey(keyToDelete);

            var deletedValue = col.FindOne(d => d.Key == keyToDelete);
            Assert.Null(deletedValue);

            Assert.Null(storage.GetBytes(keyToDelete));
            Assert.Equal(nameof(keyToStay), storage.GetString(keyToStay));
        }

        [Fact]
        public void RemoveKeyFromAreaOk()
        {
            using var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            });
            var storage = new Storage(db);

            const string areaToDeleteFrom = "areaToDeleteFrom";
            const string areaToNotTouch = "areaToNotTouch";
            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(areaToDeleteFrom, keyToDelete, new byte[] { 9, 9, 9 });
            storage.Set(areaToDeleteFrom, keyToStay, nameof(keyToStay));

            storage.Set(areaToNotTouch, keyToStay, nameof(keyToStay));

            var colToDeleteFrom = db.GetCollection<ByteEntry>(areaToDeleteFrom);
            Assert.Equal(2, colToDeleteFrom.Count());

            var colToNotTouch = db.GetCollection<ByteEntry>(areaToNotTouch);
            Assert.Equal(1, colToNotTouch.Count());

            storage.RemoveKey(areaToDeleteFrom, keyToDelete);

            var deletedValue = colToDeleteFrom.FindOne(d => d.Key == keyToDelete);
            Assert.Null(deletedValue);

            Assert.Null(storage.GetBytes(areaToDeleteFrom, keyToDelete));
            Assert.Equal(nameof(keyToStay), storage.GetString(areaToDeleteFrom, keyToStay));
            Assert.Equal(nameof(keyToStay), storage.GetString(areaToNotTouch, keyToStay));
        }

        [Fact]
        public void RemoveAreaOk()
        {
            using var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            });
            var storage = new Storage(db);

            const string areaToDelete = "areaToDelete";
            const string areaToStay = "areaToStay";
            const string keyToStay = "keyToStay";

            storage.Set(areaToDelete, "bytesKeyToDelete", new byte[] { 9, 9, 9 });
            storage.Set(areaToDelete, "stringKeyToDelete", nameof(keyToStay));

            storage.Set(areaToStay, keyToStay, nameof(keyToStay));

            var colToDelete = db.GetCollection<ByteEntry>(areaToDelete);
            Assert.Equal(2, colToDelete.Count());

            var colToStay = db.GetCollection<ByteEntry>(areaToStay);
            Assert.Equal(1, colToStay.Count());

            storage.RemoveArea(areaToDelete);

            Assert.False(db.CollectionExists(areaToDelete));

            Assert.Null(storage.GetBytes(areaToDelete, "bytesKeyToDelete"));
            Assert.Null(storage.GetString(areaToDelete, "stringKeyToDelete"));

            Assert.False(db.CollectionExists(areaToDelete));

            Assert.Equal(nameof(keyToStay), storage.GetString(areaToStay, keyToStay));
        }

        [Fact]
        public void RemoveUnknownKeyOk()
        {
            using var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            });
            var storage = new Storage(db);

            const string keyToDelete = "keyToDelete";
            const string keyToStay = "keyToStay";
            storage.Set(keyToStay, nameof(keyToStay));

            storage.RemoveKey(keyToDelete);

            Assert.Equal(nameof(keyToStay), storage.GetString(keyToStay));
        }

        [Fact]
        public void RemoveUnknownAreaOk()
        {
            using var db = new LiteDatabase(new ConnectionString(DBName)
            {
                Connection = ConnectionType.Shared
            });
            var storage = new Storage(db);

            const string keyToStay = "keyToStay";
            const string testArea = nameof(testArea);
            storage.Set(testArea, keyToStay, nameof(keyToStay));

            storage.RemoveArea("areaDoesNotExist");

            Assert.Equal(nameof(keyToStay), storage.GetString(testArea, keyToStay));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool d)
        {
            if (File.Exists(DBName))
            {
                File.Delete(DBName);
            }
        }

        internal class StringEntry
        {
            [BsonId]
            public string Key { get; set; }
            public string Value { get; set; }
        }

        internal class ByteEntry
        {
            [BsonId]
            public string Key { get; set; }
            public byte[] Value { get; set; }
        }
    }
}

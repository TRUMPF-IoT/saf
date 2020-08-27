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

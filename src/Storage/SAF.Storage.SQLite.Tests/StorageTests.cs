// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite.Tests;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using Xunit;

public class StorageTests
{
    private readonly SqliteConnection _connection;

    public StorageTests()
    {
        var cs = "Data Source=:memory:";
        _connection = new SqliteConnection(cs);
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
    public void RemoveGlobalAreaThrowsExceptionOk()
    {
        using var storage = new Storage(_connection);

        Assert.Throws<NotSupportedException>(() => storage.RemoveArea("global"));
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

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=:memory:;Version=3;")]
    [InlineData("version=3;Data Source=:memory:;")]
    [InlineData("Data Source=:memory:;VERSION='3';Mode=Memory;")]
    public void RegistrationAcceptsLegacyConnectionStrings(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSQLiteStorageInfrastructure(config => config.ConnectionString = connectionString);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IStorageInfrastructure>();

        storage.Set("key", "value");
        Assert.Equal("value", storage.GetString("key"));
    }

    [Theory]
    [InlineData("global")]
    [InlineData("named.area/name")]
    public void OverwritingValueTypeClearsPreviousValue(string area)
    {
        using var storage = new Storage(_connection);
        var bytes = new byte[] { 0, 1, 255 };

        storage.Set(area, "key", "text");
        storage.Set(area, "key", bytes);
        Assert.Null(storage.GetString(area, "key"));
        Assert.Equal(bytes, storage.GetBytes(area, "key"));

        storage.Set(area, "key", "replacement");
        Assert.Null(storage.GetBytes(area, "key"));
        Assert.Equal("replacement", storage.GetString(area, "key"));
    }

    [Fact]
    public void EmptyValuesAndMissingNamedKeysRetainBehavior()
    {
        using var storage = new Storage(_connection);

        Assert.Null(storage.GetString("missing.area", "key"));
        Assert.Null(storage.GetBytes("missing.area", "key"));
        storage.RemoveKey("missing.area", "key");

        storage.Set("emptyString", string.Empty);
        storage.Set("emptyBytes", Array.Empty<byte>());
        storage.Set("area", "emptyString", string.Empty);
        storage.Set("area", "emptyBytes", Array.Empty<byte>());

        Assert.Equal(string.Empty, storage.GetString("emptyString"));
        Assert.Equal(Array.Empty<byte>(), storage.GetBytes("emptyBytes"));
        Assert.Equal(string.Empty, storage.GetString("area", "emptyString"));
        Assert.Equal(Array.Empty<byte>(), storage.GetBytes("area", "emptyBytes"));
        Assert.Null(storage.GetString("area", "missing"));
        Assert.Null(storage.GetBytes("area", "missing"));
    }

    [Fact]
    public void RegisteredStoragePersistsValuesWithQuotedLegacyConnectionString()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saf-Version=3;{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        var bytes = new byte[] { 0, 1, 255 };

        try
        {
            var services = new ServiceCollection();
            services.AddSQLiteStorageInfrastructure(config => config.ConnectionString = $"{connectionString};Version=3;");
            using (var provider = services.BuildServiceProvider())
            {
                var storage = provider.GetRequiredService<IStorageInfrastructure>();
                storage.Set("text", "global text");
                storage.Set("bytes", bytes);
                storage.Set("legacy.area/name", "text", "named text");
                storage.Set("legacy.area/name", "bytes", bytes);
            }

            using var reopenedProvider = services.BuildServiceProvider();
            var reopened = reopenedProvider.GetRequiredService<IStorageInfrastructure>();
            Assert.Equal("global text", reopened.GetString("text"));
            Assert.Equal(bytes, reopened.GetBytes("bytes"));
            Assert.Equal("named text", reopened.GetString("legacy_area_name", "text"));
            Assert.Equal(bytes, reopened.GetBytes("legacy.area/name", "bytes"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DatabaseWrittenBySystemDataSQLiteRemainsReadableAndWritable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saf-legacy-{Guid.NewGuid():N}.db");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-system-data-sqlite.db"), path);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        var bytes = new byte[] { 0, 1, 2, 255 };

        try
        {
            using (var storage = new Storage(new SqliteConnection(connectionString)))
            {
                Assert.Equal("legacy global text", storage.GetString("text"));
                Assert.Equal(bytes, storage.GetBytes("bytes"));
                Assert.Equal("legacy named text", storage.GetString("legacy.area/name", "text"));
                Assert.Equal(bytes, storage.GetBytes("legacy.area/name", "bytes"));
                Assert.Equal("Grüße 你好", storage.GetString("legacy.area/name", "unicode"));
                storage.Set("text", "updated global text");
                storage.Set("bytes", new byte[] { 42 });
                storage.Set("legacy.area/name", "text", "updated named text");
                storage.Set("legacy.area/name", "bytes", new byte[] { 43 });
            }

            using var reopened = new Storage(new SqliteConnection(connectionString));
            Assert.Equal("updated global text", reopened.GetString("text"));
            Assert.Equal(new byte[] { 42 }, reopened.GetBytes("bytes"));
            Assert.Equal("updated named text", reopened.GetString("legacy.area/name", "text"));
            Assert.Equal(new byte[] { 43 }, reopened.GetBytes("legacy.area/name", "bytes"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MixedConcurrentCallsAreSafe()
    {
        using var storage = new Storage(_connection);
        var sharedBytes = new byte[] { 0, 1, 255 };
        storage.Set("shared", "text", "shared text");
        storage.Set("shared", "bytes", sharedBytes);

        Parallel.For(0, 32, worker =>
        {
            var area = $"worker.area/{worker}";
            var globalKey = $"worker{worker}";
            for (var iteration = 0; iteration < 25; iteration++)
            {
                var value = $"value {worker}:{iteration}";
                var bytes = new byte[] { (byte)worker, (byte)iteration };
                Assert.Equal("shared text", storage.GetString("shared", "text"));
                Assert.Equal(sharedBytes, storage.GetBytes("shared", "bytes"));

                storage.Set(globalKey, value);
                Assert.Equal(value, storage.GetString(globalKey));
                storage.Set(globalKey, bytes);
                Assert.Equal(bytes, storage.GetBytes(globalKey));
                Assert.Null(storage.GetString(globalKey));
                storage.RemoveKey(globalKey);
                Assert.Null(storage.GetBytes(globalKey));

                storage.Set(area, "text", value);
                storage.Set(area, "bytes", bytes);
                Assert.Equal(value, storage.GetString(area, "text"));
                Assert.Equal(bytes, storage.GetBytes(area, "bytes"));
                storage.RemoveKey(area, "text");
                Assert.Null(storage.GetString(area, "text"));
                storage.RemoveArea(area);
                Assert.Null(storage.GetBytes(area, "bytes"));
            }
        });
    }

    [Theory]
    [InlineData("x; DROP TABLE global; --")]
    [InlineData("area \"with\" quotes")]
    [InlineData("hyphen-area")]
    public void AreaNamesAreTreatedAsIdentifiers(string area)
    {
        using var storage = new Storage(_connection);
        storage.Set("key", "global value");

        storage.Set(area, "key", "area value");
        Assert.Equal("area value", storage.GetString(area, "key"));
        storage.RemoveKey(area, "key");
        Assert.Null(storage.GetString(area, "key"));
        storage.RemoveArea(area);

        Assert.Equal("global value", storage.GetString("key"));
    }

    [Fact]
    public void BundledNativeSqliteLibraryIsUsed()
    {
        using var storage = new Storage(_connection);
        storage.Set("native", "loaded");

        Assert.Equal("loaded", storage.GetString("native"));
        Assert.Equal("e_sqlite3", SQLitePCL.raw.GetNativeLibraryName());
    }

    [Fact]
    public void EncryptedConnectionStringIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSQLiteStorageInfrastructure(config => config.ConnectionString = "Data Source=:memory:;Version=3;Password=secret;");
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IStorageInfrastructure>());
    }

    [Fact]
    public void NullValuesAreStoredAsNull()
    {
        using var storage = new Storage(_connection);
        storage.Set("key", "value");
        storage.Set("area", "key", "value");

        storage.Set("key", (string)null!);
        storage.Set("area", "key", (byte[])null!);

        Assert.Null(storage.GetString("key"));
        Assert.Null(storage.GetBytes("area", "key"));
        Assert.Null(storage.GetString(null!));
        storage.RemoveKey(null!);
    }

    [Fact]
    public void DisposeReleasesDatabaseFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saf-dispose-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddSQLiteStorageInfrastructure(config => config.ConnectionString = $"Data Source={path};");

        using (var provider = services.BuildServiceProvider())
        {
            provider.GetRequiredService<IStorageInfrastructure>().Set("key", "value");
        }

        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("GLOBAL")]
    [InlineData("Global")]
    public void RemoveGlobalAreaIgnoresCase(string area)
    {
        using var storage = new Storage(_connection);
        storage.Set("key", "value");

        Assert.Throws<NotSupportedException>(() => storage.RemoveArea(area));
        Assert.Equal("value", storage.GetString("key"));
    }

    [Fact]
    public void AreaNamesIgnoreCase()
    {
        using var storage = new Storage(_connection);
        storage.Set("MyArea", "a", "1");
        storage.Set("myarea", "b", "2");

        Assert.Equal("2", storage.GetString("MyArea", "b"));
        Assert.Equal("1", storage.GetString("myarea", "a"));
        storage.RemoveKey("MYAREA", "b");
        Assert.Null(storage.GetString("myarea", "b"));
    }

}
// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Xunit;

namespace SAF.Messaging.Cde.Tests;

public class StorageTests : IClassFixture<CdeFixture>
{
    private readonly CdeFixture _cde;

    public StorageTests(CdeFixture cde)
    {
        _cde = cde;
    }

    [Fact]
    public void GlobalSetGetStringOk()
    {
        const string keyPrefix = "GlobalSetGetString";

        var storage = _cde.Storage;

        var setResult = storage.Set($"{keyPrefix}1", "myTestValue1");

        Assert.Equal(storage, setResult);

        storage.Set($"{keyPrefix}2", "myTestValue2")
            .Set($"{keyPrefix}3", "myTestValue3")
            .Set($"{keyPrefix}4.sk1", "myTestValue4");

        var getResult1 = storage.GetString($"{keyPrefix}1");
        var getResult2 = storage.GetString($"{keyPrefix}2");
        var getResult3 = storage.GetString($"{keyPrefix}3");
        var getResult4 = storage.GetString($"{keyPrefix}4.sk1");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);
        Assert.NotNull(getResult4);

        Assert.Equal("myTestValue1", getResult1);
        Assert.Equal("myTestValue2", getResult2);
        Assert.Equal("myTestValue3", getResult3);
        Assert.Equal("myTestValue4", getResult4);
    }

    [Fact]
    public void AreaSetGetStringOk()
    {
        const string keyPrefix = "AreaSetGetString";

        var storage = _cde.Storage;

        var setResult = storage.Set("Area", $"{keyPrefix}1", "myTestValue1");

        Assert.Equal(storage, setResult);

        storage.Set("Area", $"{keyPrefix}2", "myTestValue2")
            .Set("Area", $"{keyPrefix}3", "myTestValue3")
            .Set("Area", $"{keyPrefix}4.sk1", "myTestValue4");

        var getResult1 = storage.GetString("Area", $"{keyPrefix}1");
        var getResult2 = storage.GetString("Area", $"{keyPrefix}2");
        var getResult3 = storage.GetString("Area", $"{keyPrefix}3");
        var getResult4 = storage.GetString("Area", $"{keyPrefix}4.sk1");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);
        Assert.NotNull(getResult4);

        Assert.Equal("myTestValue1", getResult1);
        Assert.Equal("myTestValue2", getResult2);
        Assert.Equal("myTestValue3", getResult3);
        Assert.Equal("myTestValue4", getResult4);
    }

    [Fact]
    public void GlobalSetGetBytesOk()
    {
        var bytes1 = new byte[] { 1, 2, 3 };
        var bytes2 = new byte[] { 4, 5, 6 };
        var bytes3 = new byte[] { 7, 8, 9 };
        var bytes4 = new byte[] { 10, 11, 12 };

        const string keyPrefix = "GlobalSetGetBytes";
        var storage = _cde.Storage;

        var setResult = storage.Set($"{keyPrefix}1", bytes1);

        Assert.Equal(storage, setResult);

        storage.Set($"{keyPrefix}2", bytes2)
            .Set($"{keyPrefix}3", bytes3)
            .Set($"{keyPrefix}4.sk1", bytes4);

        var getResult1 = storage.GetBytes($"{keyPrefix}1");
        var getResult2 = storage.GetBytes($"{keyPrefix}2");
        var getResult3 = storage.GetBytes($"{keyPrefix}3");
        var getResult4 = storage.GetBytes($"{keyPrefix}4.sk1");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);
        Assert.NotNull(getResult4);

        Assert.True(bytes1.SequenceEqual(getResult1));
        Assert.True(bytes2.SequenceEqual(getResult2));
        Assert.True(bytes3.SequenceEqual(getResult3));
        Assert.True(bytes4.SequenceEqual(getResult4));
    }

    [Fact]
    public void AreaSetGetBytesOk()
    {
        var bytes1 = new byte[] { 1, 2, 3 };
        var bytes2 = new byte[] { 4, 5, 6 };
        var bytes3 = new byte[] { 7, 8, 9 };
        var bytes4 = new byte[] { 10, 11, 12 };

        const string keyPrefix = "AreaSetGetBytes";
        var storage = _cde.Storage;

        var setResult = storage.Set("Area", $"{keyPrefix}1", bytes1);

        Assert.Equal(storage, setResult);

        storage.Set("Area", $"{keyPrefix}2", bytes2)
            .Set("Area", $"{keyPrefix}3", bytes3)
            .Set("Area", $"{keyPrefix}4.sk1", bytes4);

        var getResult1 = storage.GetBytes("Area", $"{keyPrefix}1");
        var getResult2 = storage.GetBytes("Area", $"{keyPrefix}2");
        var getResult3 = storage.GetBytes("Area", $"{keyPrefix}3");
        var getResult4 = storage.GetBytes("Area", $"{keyPrefix}4.sk1");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);
        Assert.NotNull(getResult4);

        Assert.True(bytes1.SequenceEqual(getResult1));
        Assert.True(bytes2.SequenceEqual(getResult2));
        Assert.True(bytes3.SequenceEqual(getResult3));
        Assert.True(bytes4.SequenceEqual(getResult4));
    }

    [Fact]
    public void GlobalSetUpdateGetStringOk()
    {
        var storage = _cde.Storage;

        storage.Set("myTestKey1", "myTestValue1")
            .Set("myTestKey2", "myTestValue2")
            .Set("myTestKey3", "myTestValue3");

        storage.Set("myTestKey1", "myUpdatedTestValue1")
            .Set("myTestKey2", "myUpdatedTestValue2")
            .Set("myTestKey3", "myUpdatedTestValue3");

        var getResult1 = storage.GetString("myTestKey1");
        var getResult2 = storage.GetString("myTestKey2");
        var getResult3 = storage.GetString("myTestKey3");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);

        Assert.Equal("myUpdatedTestValue1", getResult1);
        Assert.Equal("myUpdatedTestValue2", getResult2);
        Assert.Equal("myUpdatedTestValue3", getResult3);
    }

    [Fact]
    public void GlobalSetUpdateGetBytesOk()
    {
        var bytes1 = new byte[] { 1, 2, 3 };
        var bytes2 = new byte[] { 4, 5, 6 };
        var bytes3 = new byte[] { 7, 8, 9 };

        var storage = _cde.Storage;

        storage.Set("myTestByteKey1", bytes1)
            .Set("myTestByteKey2", bytes2)
            .Set("myTestByteKey3", bytes3);

        var updateBytes1 = new byte[] { 10, 20, 30 };
        var updateBytes2 = new byte[] { 40, 50, 60 };
        var updateBytes3 = new byte[] { 70, 80, 90 };

        storage.Set("myTestByteKey1", updateBytes1)
            .Set("myTestByteKey2", updateBytes2)
            .Set("myTestByteKey3", updateBytes3);

        var getResult1 = storage.GetBytes("myTestByteKey1");
        var getResult2 = storage.GetBytes("myTestByteKey2");
        var getResult3 = storage.GetBytes("myTestByteKey3");

        Assert.NotNull(getResult1);
        Assert.NotNull(getResult2);
        Assert.NotNull(getResult3);

        Assert.True(updateBytes1.SequenceEqual(getResult1));
        Assert.True(updateBytes2.SequenceEqual(getResult2));
        Assert.True(updateBytes3.SequenceEqual(getResult3));
    }

    [Fact]
    public void GlobalGetUnknownStringOk()
    {
        var storage = _cde.Storage;
        var getResult = storage.GetString("myUnknownTestKey1");
        Assert.Null(getResult);
    }

    [Fact]
    public void GlobalGetUnknownBytesOk()
    {
        var storage = _cde.Storage;
        var getResult = storage.GetBytes("myUnknownByteTestKey1");
        Assert.Null(getResult);
    }

    [Fact]
    public void RemoveKeyOk()
    {
        var storage = _cde.Storage;

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
        var storage = _cde.Storage;

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
        var storage = _cde.Storage;

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
        var storage = _cde.Storage;

        Assert.Throws<NotSupportedException>(() => storage.RemoveArea("global"));
    }

    [Fact]
    public void RemoveUnknownKeyOk()
    {
        var storage = _cde.Storage;

        const string keyToDelete = "keyToDelete";
        const string keyToStay = "keyToStay";
        storage.Set(keyToStay, nameof(keyToStay));

        storage.RemoveKey(keyToDelete);

        Assert.Equal(nameof(keyToStay), storage.GetString(keyToStay));
    }

    [Fact]
    public void RemoveUnknownAreaOk()
    {
        var storage = _cde.Storage;

        const string keyToStay = "keyToStay";
        const string testArea = nameof(testArea);
        storage.Set(testArea, keyToStay, nameof(keyToStay));

        storage.RemoveArea("areaDoesNotExist");

        Assert.Equal(nameof(keyToStay), storage.GetString(testArea, keyToStay));
    }
}
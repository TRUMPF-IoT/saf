// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.Serialization;
using SAF.Toolbox.Serialization;
using Xunit;

public class JsonSerializerTests
{
    [Fact]
    public void ObjectsAreCamelCased()
    {
        var objectToSerialize = new
        {
            A = 123,
            b = 234
        };

        var json = JsonSerializer.Serialize(objectToSerialize);
        Assert.Equal("{\"a\":123,\"b\":234}", json, StringComparer.Ordinal);
    }

    [Fact]
    public void DictionariesAreNotCamelCased()
    {
        var objectToSerialize = new Dictionary<string, int>()
        {
            { "A", 123 },
            { "b", 234 }
        };

        var json = JsonSerializer.Serialize(objectToSerialize);
        Assert.Equal("{\"A\":123,\"b\":234}", json, StringComparer.Ordinal);
    }

    [Fact]
    public void UsingDefaultJsonIsNotCamelCased()
    {
        const int expectedInt = 123;
        const int expectedLong = 1234;
        const string expectedString = "hello";

        var objectToSerialize = new TestCaseOrdererAttribute
        {
            AProperty = expectedInt,
            IsCamelcasedInSaf = expectedLong,
            ButPascalCasedInQds = expectedString
        };

        var jsonWithToolboxConverter = JsonSerializer.Serialize(objectToSerialize);
        var jsonWithDefaultConverter = System.Text.Json.JsonSerializer.Serialize(objectToSerialize);

        //Test Serializer
        Assert.Equal($"{{\"aProperty\":{expectedInt},\"isCamelcasedInSaf\":{expectedLong},\"butPascalCasedInQds\":\"{expectedString}\"}}", jsonWithToolboxConverter, StringComparer.Ordinal);
        Assert.Equal($"{{\"AProperty\":{expectedInt},\"IsCamelcasedInSaf\":{expectedLong},\"ButPascalCasedInQds\":\"{expectedString}\"}}", jsonWithDefaultConverter, StringComparer.Ordinal);

        //Ensure that both strings can still be deserialized by both serializers
        foreach (var serializedString in new[] { jsonWithToolboxConverter, jsonWithDefaultConverter })
        {
            var safClass = JsonSerializer.Deserialize<TestCaseOrdererAttribute>(serializedString);
            var defaultClass = System.Text.Json.JsonSerializer.Deserialize<TestCaseOrdererAttribute>(serializedString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(safClass);
            Assert.NotNull(defaultClass);
            Assert.Equal(expectedInt, defaultClass.AProperty);
            Assert.Equal(expectedLong, defaultClass.IsCamelcasedInSaf);
            Assert.Equal(expectedString, defaultClass.ButPascalCasedInQds);
            Assert.Equal(expectedInt, safClass.AProperty);
            Assert.Equal(expectedLong, safClass.IsCamelcasedInSaf);
            Assert.Equal(expectedString, safClass.ButPascalCasedInQds);
        }
    }

    [Fact]
    public void SerializeEmptyConverterList()
    {
        const int expectedInt = 123;
        const int expectedLong = 123456789;
        const string expectedString = "hello";

        var objectToSerialize = new TestCaseOrdererAttribute
        {
            AProperty = expectedInt,
            IsCamelcasedInSaf = expectedLong,
            ButPascalCasedInQds = expectedString
        };

        var jsonWithToolboxConverter = JsonSerializer.Serialize(objectToSerialize, converters: new List<IJsonObjectConverter>().ToArray());

        //Test Serializer
        Assert.Equal($"{{\"aProperty\":{expectedInt},\"isCamelcasedInSaf\":{expectedLong},\"butPascalCasedInQds\":\"{expectedString}\"}}", jsonWithToolboxConverter, StringComparer.Ordinal);
    }

    [Fact]
    public void DeserializeWithType()
    {
        const int expectedInt = 123;
        const int expectedLong = 1234;
        const string expectedString = "hello";

        var jsonWithToolboxConverter = (TestCaseOrdererAttribute)JsonSerializer.Deserialize("{\"aProperty\":" + expectedInt + ",\"isCamelcasedInSaf\":" + expectedLong + ",\"butPascalCasedInQds\":\"" + expectedString + "\"}", typeof(TestCaseOrdererAttribute))!;

        //Test Serializer
        Assert.Equal(expectedInt, jsonWithToolboxConverter.AProperty);
        Assert.Equal(expectedLong, jsonWithToolboxConverter.IsCamelcasedInSaf);
        Assert.Equal(expectedString, jsonWithToolboxConverter.ButPascalCasedInQds);
    }

    [Fact]
    public void DeserializeWithTypeEmptyConverterList()
    {
        const int expectedInt = 123;
        const int expectedLong = 1234;
        const string expectedString = "hello";

        var jsonWithToolboxConverter =
            (TestCaseOrdererAttribute?)JsonSerializer.Deserialize("{\"aProperty\":" + expectedInt + ",\"isCamelcasedInSaf\":" + expectedLong + ",\"butPascalCasedInQds\":\"" + expectedString + "\"}",
                typeof(TestCaseOrdererAttribute),
                new List<IJsonObjectConverter>().ToArray());

        //Test Serializer
        Assert.Equal(expectedInt, jsonWithToolboxConverter!.AProperty);
        Assert.Equal(expectedLong, jsonWithToolboxConverter.IsCamelcasedInSaf);
        Assert.Equal(expectedString, jsonWithToolboxConverter.ButPascalCasedInQds);
    }

    [Fact]
    public void DeserializeWithEmptyConverterList()
    {
        const int expectedInt = 123;
        const int expectedLong = 1234;
        const string expectedString = "hello";

        var jsonWithToolboxConverter = JsonSerializer.Deserialize<TestCaseOrdererAttribute>("{\"aProperty\":" + expectedInt + ",\"isCamelcasedInSaf\":" + expectedLong + ",\"butPascalCasedInQds\":\"" + expectedString + "\"}",
            new List<IJsonObjectConverter>().ToArray());

        //Test Serializer
        Assert.Equal(expectedInt, jsonWithToolboxConverter!.AProperty);
        Assert.Equal(expectedLong, jsonWithToolboxConverter.IsCamelcasedInSaf);
        Assert.Equal(expectedString, jsonWithToolboxConverter.ButPascalCasedInQds);
    }

    [Fact]
    public void ObjectToInferredTypesConverterSerialization()
    {
        var objectDictionary = new Dictionary<string, object>
        {
            { "boolean", true },
            { "datetimeoffset", new DateTimeOffset(2023, 10, 19, 0, 0, 0, TimeSpan.FromHours(2)) },
            { "datetime", new DateTime(2023, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            { "number", 1234 },
            { "double", 12.34d },
            { "string", "A string" }
        };

        var json = JsonSerializer.Serialize(objectDictionary);

        const string expectedJson = "{\"boolean\":true,\"datetimeoffset\":\"2023-10-19T00:00:00+02:00\",\"datetime\":\"2023-10-19T00:00:00Z\",\"number\":1234,\"double\":12.34,\"string\":\"A string\"}";
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void ObjectToInferredTypesConverterDeserialization()
    {
        const string json = "{\"boolean\":true,\"datetimeoffset\":\"2023-10-19T00:00:00+02:00\",\"datetime\":\"2023-10-19 00:00:00\",\"number\":1234,\"double\":12.34,\"string\":\"A string\"}";

        var objectDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

        Assert.True(objectDictionary["boolean"] is bool);
        Assert.True(Convert.ToBoolean(objectDictionary["boolean"]));
        Assert.True(objectDictionary["datetimeoffset"] is DateTimeOffset);
        Assert.Equal(TimeSpan.FromHours(2), ((DateTimeOffset)objectDictionary["datetimeoffset"]).Offset);
        Assert.Equal(new DateTime(2023, 10, 19, 0, 0, 0, DateTimeKind.Unspecified), ((DateTimeOffset)objectDictionary["datetimeoffset"]).DateTime);
        Assert.True(objectDictionary["datetime"] is DateTimeOffset);
        Assert.Equal(new DateTimeOffset(new DateTime(2023, 10, 19, 0, 0, 0, DateTimeKind.Local)).Offset, ((DateTimeOffset)objectDictionary["datetime"]).Offset);
        Assert.True(objectDictionary["number"] is long);
        Assert.Equal(1234L, objectDictionary["number"]);
        Assert.True(objectDictionary["double"] is double);
        Assert.Equal(12.34, objectDictionary["double"]);
        Assert.True(objectDictionary["string"] is string);
        Assert.Equal("A string", objectDictionary["string"]);
    }

    [Fact]
    public void ObjectToInferredTypesConverterSerializesObject()
    {
        var json = JsonSerializer.Serialize(new object());
        Assert.Equal("{}", json);
    }

    [Fact]
    public void DeserializeNullDateTime()
    {
        var testObject = JsonSerializer.Deserialize<TestCaseFields>($"{{\"datetime\": null,\"datetimeoffset\": null}}");

        Assert.NotNull(testObject);

        Assert.Null(testObject.DateTime);
        Assert.Null(testObject.DateTimeOffset);
    }

    [Theory]
    [InlineData("2023-11-09 06:07:08", DateTimeKind.Unspecified)]
    [InlineData("2023-11-09T06:07:08Z", DateTimeKind.Utc)]
    [InlineData("2023-11-09T06:07:08+02:00", DateTimeKind.Local)]
    public void DeserializeDateTimeFormats(string datetimeValue, DateTimeKind expectedKind)
    {
        var testObject = JsonSerializer.Deserialize<TestCaseFields>(
            $"{{\"datetime\": \"{datetimeValue}\",\"datetimeoffset\": \"{datetimeValue}\"}}");

        Assert.NotNull(testObject);

        var localTimeZoneDateTime = new DateTimeOffset(new DateTime(2023, 11, 9, 6, 7, 8));

        Assert.NotNull(testObject.DateTime);
        Assert.Equal(expectedKind, testObject.DateTime.Value.Kind);
        Assert.Equal(2023, testObject.DateTime.Value.Year);
        Assert.Equal(11, testObject.DateTime.Value.Month);
        Assert.Equal(9, testObject.DateTime.Value.Day);
        Assert.Equal(expectedKind == DateTimeKind.Local
            ? 6 - 2 + localTimeZoneDateTime.Offset.TotalMilliseconds / (1000 * 60 * 60)
            : 6, testObject.DateTime.Value.Hour);
        Assert.Equal(7, testObject.DateTime.Value.Minute);
        Assert.Equal(8, testObject.DateTime.Value.Second);

        Assert.NotNull(testObject.DateTimeOffset);
        Assert.Equal(expectedKind, testObject.DateTime.Value.Kind);
        Assert.Equal(2023, testObject.DateTimeOffset.Value.Year);
        Assert.Equal(11, testObject.DateTimeOffset.Value.Month);
        Assert.Equal(9, testObject.DateTimeOffset.Value.Day);
        Assert.Equal(6, testObject.DateTimeOffset.Value.Hour);
        Assert.Equal(7, testObject.DateTimeOffset.Value.Minute);
        Assert.Equal(8, testObject.DateTimeOffset.Value.Second);
        Assert.Equal(expectedKind == DateTimeKind.Utc
            ? TimeSpan.Zero
            : expectedKind == DateTimeKind.Unspecified
                ? localTimeZoneDateTime.Offset
                : TimeSpan.FromHours(2), testObject.DateTimeOffset.Value.Offset);
    }

    [Fact]
    public void TestCaseFieldsCompare()
    {
        var testobjekt = new TestCaseFields { AnInteger = 1, aString = "Wert" };
        var jsonWithToolboxConverter = JsonSerializer.Serialize(testobjekt);
        Assert.Equal(@"{""anInteger"":1,""aString"":""Wert""}", jsonWithToolboxConverter);
        var objectDeserialized = JsonSerializer.Deserialize<TestCaseFields>(jsonWithToolboxConverter)!;
        Assert.Equal(testobjekt.AnInteger, objectDeserialized.AnInteger);
        Assert.Equal(testobjekt.aString, objectDeserialized.aString);
    }

    [Fact]
    public void Comments()
    {
        var jsonWithComment = "{ \r\n//With comment\r\n\"anInteger\":1, /* second comment */\"aString\":\"Wert\"\r\n}";
        var obj = JsonSerializer.Deserialize<TestCaseFields>(jsonWithComment);
        Assert.NotNull(obj);
    }

    [Fact]
    public void TrailingComma()
    {
        var jsonWithTrailingComma = "{ \"anInteger\":1, \"aString\":\"Wert\",}";
        var obj = JsonSerializer.Deserialize<TestCaseFields>(jsonWithTrailingComma);
        Assert.NotNull(obj);

        jsonWithTrailingComma = "{ \"anInteger\":1, \"aString\":\"Wert\",,}";
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<TestCaseFields>(jsonWithTrailingComma));
    }

    [Fact]
    public void SerializeWithCustomConverterRunsConverter()
    {
        var converter = new CustomJsonConverter();
        var obj = new TestCaseCustomConverterType
        {
            ObjectDictionary = new Dictionary<string, object> { { "boolean", false } },
            CustomType = new TestCaseCustomType { IntValue = 1234, StringValue = "5678" }
        };

        var json = JsonSerializer.Serialize(obj, converter);
        Assert.Equal(1, converter.SerializeCalled);

        Assert.Equal("{\"objectDictionary\":{\"boolean\":false},\"customType\":{\"intValue\":1234,\"stringValue\":\"5678\"}}", json);
    }

    [Fact]
    public void DeserializeWithCustomConverterRunsConverter()
    {
        var converter = new CustomJsonConverter();
       
        const string json = "{\"objectDictionary\":{\"boolean\":true},\"customType\":{\"intValue\":1234}}";

        var obj = JsonSerializer.Deserialize<TestCaseCustomConverterType>(json, converter)!;
        Assert.Equal(1, converter.DeserializeCalled);

        Assert.True(obj.ObjectDictionary["boolean"] is bool);
        Assert.True(Convert.ToBoolean(obj.ObjectDictionary["boolean"]));
        Assert.Equal(1234, obj.CustomType.IntValue);
        Assert.Null(obj.CustomType.StringValue);
    }

    private class TestCaseOrdererAttribute
    {
        public int AProperty { get; set; }
        public int IsCamelcasedInSaf { get; set; }
        public string ButPascalCasedInQds { get; set; } = default!;
    }

    private class TestCaseFields
    {
        public int AnInteger { get; set; }
        public string? aString { get; set; }
        public DateTime? DateTime { get; set; }
        public DateTimeOffset? DateTimeOffset { get; set; }
    }

    private class TestCaseCustomConverterType
    {
        public Dictionary<string, object> ObjectDictionary { get; set; } = new() {{"boolean", true}};
        public TestCaseCustomType CustomType { get; set; } = new();
    }

    private class TestCaseCustomType
    {
        public int IntValue { get; set; }
        public string? StringValue { get; set; }
    }

    private class CustomJsonConverter : IJsonObjectConverter<TestCaseCustomType>
    {
        internal int DeserializeCalled { get; private set; }
        internal int SerializeCalled { get; private set; }

        public bool CanRead => true;

        public bool CanWrite => true;

        public bool CanConvert(Type objectType) => objectType == typeof(TestCaseCustomType);

        public string SerializeObject(object sourceObject) => SerializeObject((TestCaseCustomType)sourceObject);

        public object DeserializeObject(Type objectType, string jsonObject) => DeserializeObject(jsonObject);

        public string SerializeObject(TestCaseCustomType sourceObject)
        {
            SerializeCalled++;
            return JsonSerializer.Serialize(sourceObject);
        }

        public TestCaseCustomType DeserializeObject(string jsonObject)
        {
            DeserializeCalled++;
            return JsonSerializer.Deserialize<TestCaseCustomType>(jsonObject)!;
        }
    }
}
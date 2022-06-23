// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace SAF.Toolbox.Tests
{
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
                var defaultClass = System.Text.Json.JsonSerializer.Deserialize<TestCaseOrdererAttribute>(serializedString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true});

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

        private class TestCaseOrdererAttribute
        {
            public int AProperty { get; set; }
            public int IsCamelcasedInSaf { get; set; }
            public string ButPascalCasedInQds { get; set; }
        }

        [Fact]
        public void TestCaseFieldsCompare()
        {
            var testobjekt = new TestCaseFields { AnInteger = 1, aString = "Wert" };
            var jsonWithToolboxConverter = JsonSerializer.Serialize(testobjekt);
            Assert.Equal(@"{""anInteger"":1,""aString"":""Wert""}", jsonWithToolboxConverter);
            var objectDeserialized = JsonSerializer.Deserialize<TestCaseFields>(jsonWithToolboxConverter);
            Assert.Equal(testobjekt.AnInteger, objectDeserialized.AnInteger);
            Assert.Equal(testobjekt.aString, objectDeserialized.aString);
        }

        private class TestCaseFields
        {
            public int AnInteger;
            public string aString;
        }

    }
}

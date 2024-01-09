// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.Serialization;
using System.Text.Json;
using SAF.Toolbox.Serialization;
using NSubstitute;
using Xunit;
using System.Text;

public class JsonObjectConverterTest
{
    [Fact]
    public void CanReadWrite()
    {
        var jsonObjectConverter = Substitute.For<IJsonObjectConverter>();
        jsonObjectConverter.CanRead.Returns(true);
        jsonObjectConverter.CanWrite.Returns(false);

        var converter = new JsonObjectConverter(jsonObjectConverter);
        Assert.True(converter.CanRead);
        Assert.False(converter.CanWrite);
    }

    [Fact]
    public void CanConvertTrue()
    {
        var jsonObjectConverter = Substitute.For<IJsonObjectConverter>();
        jsonObjectConverter.CanConvert(Arg.Any<Type>()).Returns(true);

        var converter = new JsonObjectConverter(jsonObjectConverter);
        Assert.True(converter.CanConvert(typeof(string)));
    }

    [Fact]
    public void CanConvertFalse()
    {
        var jsonObjectConverter = Substitute.For<IJsonObjectConverter>();
        jsonObjectConverter.CanConvert(Arg.Any<Type>()).Returns(false);

        var converter = new JsonObjectConverter(jsonObjectConverter);
        Assert.False(converter.CanConvert(typeof(string)));
    }

    [Fact]
    public void Write()
    {
        var value = new Test { Value = 55 };
        var jsonObjectConverter = Substitute.For<IJsonObjectConverter>();
        jsonObjectConverter.CanWrite.Returns(false);
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);

        var converter = new JsonObjectConverter(jsonObjectConverter);
        converter.Write(writer, value, new JsonSerializerOptions());

        writer.Flush();
        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("{\"value\":55}", json);
    }


    [Fact]
    public void Read()
    {
        var jsonObjectConverter = Substitute.For<IJsonObjectConverter>();
        jsonObjectConverter.CanRead.Returns(false);
        byte[] wert = "{\"value\":55}"u8.ToArray();
        var reader = new Utf8JsonReader(wert);

        var converter = new JsonObjectConverter(jsonObjectConverter);
        var obj = (Test)converter.Read(ref reader, typeof(Test), new JsonSerializerOptions());

        Assert.Equal(55, obj.Value);
    }

    private class Test
    {
        public int Value { get; init; }
    }
}
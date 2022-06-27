using System;
using System.Collections.Generic;
using System.Buffers;
using System.Text.Json;
using SAF.Toolbox.Serialization;
using NSubstitute;
using Xunit;
using System.Text;
using System.IO;

namespace SAF.Toolbox.Tests
{
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
            byte[] wert = Encoding.UTF8.GetBytes("{\"value\":55}");
            var reader = new Utf8JsonReader(wert);

            var converter = new JsonObjectConverter(jsonObjectConverter);
            var obj = (Test)converter.Read(ref reader,typeof(Test), new JsonSerializerOptions());

            Assert.Equal(55, obj.Value);
        }

        private class Test
        {
            public int Value { get; set; }
        }
    }
}

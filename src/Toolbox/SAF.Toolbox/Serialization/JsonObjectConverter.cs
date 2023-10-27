// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SAF.Toolbox.Serialization
{
    public class JsonObjectConverter : JsonConverter<object>
    {
        private readonly IJsonObjectConverter _converter;

        public JsonObjectConverter(IJsonObjectConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public bool CanWrite => _converter.CanWrite;
        public bool CanRead => _converter.CanRead;

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var jsonObject = CanWrite ? _converter.SerializeObject(value) : JsonSerializer.Serialize(value);
            writer.WriteRawValue(jsonObject);
        }

        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonObject = JsonNode.Parse(ref reader)?.ToString();
            return CanRead ? _converter.DeserializeObject(typeToConvert, jsonObject) : JsonSerializer.Deserialize(jsonObject, typeToConvert);
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return _converter.CanConvert(typeToConvert);
        }
    }
}
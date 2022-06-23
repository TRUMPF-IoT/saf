// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonConverter = System.Text.Json.Serialization.JsonConverter<object>;

namespace SAF.Toolbox.Serialization
{
    public class JsonObjectConverter : JsonConverter
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
            var jsonObject = _converter.SerializeObject(value);
            writer.WriteRawValue(jsonObject);
        }

        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonObject = JsonNode.Parse(ref reader).ToString();
            return _converter.DeserializeObject(typeToConvert, jsonObject);
        }

        public override bool CanConvert(Type objectType)
        {
            return _converter.CanConvert(objectType);
        }
    }
}
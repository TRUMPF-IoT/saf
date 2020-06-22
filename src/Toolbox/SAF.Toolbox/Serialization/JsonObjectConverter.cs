// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SAF.Toolbox.Serialization
{
    public class JsonObjectConverter : JsonConverter
    {
        private readonly IJsonObjectConverter _converter;

        public JsonObjectConverter(IJsonObjectConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public override bool CanWrite => _converter.CanWrite;
        public override bool CanRead => _converter.CanRead;

        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            var jsonObject = _converter.SerializeObject(value);
            writer.WriteRawValue(jsonObject);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var jsonObject = JToken.ReadFrom(reader).ToString();
            return _converter.DeserializeObject(objectType, jsonObject);
        }

        public override bool CanConvert(Type objectType)
        {
            return _converter.CanConvert(objectType);
        }
    }
}
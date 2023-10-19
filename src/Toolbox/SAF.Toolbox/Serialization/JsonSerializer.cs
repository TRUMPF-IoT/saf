// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonTransformer = System.Text.Json.JsonSerializer;
using JsonConverter = System.Text.Json.Serialization.JsonConverter<object>;

namespace SAF.Toolbox.Serialization
{
    public static class JsonSerializer
    {
        private static readonly JsonSerializerOptions CamelCaseDefaultSettings = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = false,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
        };

        public static string Serialize(object obj)
            => JsonTransformer.Serialize(obj, CamelCaseDefaultSettings);

        public static string Serialize(object obj, params IJsonObjectConverter[] converters)
            => JsonTransformer.Serialize(obj, DefaultSettingsWithConverters(converters));

        public static T Deserialize<T>(string json)
            => JsonTransformer.Deserialize<T>(json, CamelCaseDefaultSettings);

        public static object Deserialize(string json, Type type)
            => JsonTransformer.Deserialize(json, type, CamelCaseDefaultSettings);

        public static T Deserialize<T>(string json, params IJsonObjectConverter[] converters)
            => JsonTransformer.Deserialize<T>(json, DefaultSettingsWithConverters(converters));

        public static object Deserialize(string json, Type type, params IJsonObjectConverter[] converters)
            => JsonTransformer.Deserialize(json, type, DefaultSettingsWithConverters(converters));

        private static JsonSerializerOptions DefaultSettingsWithConverters(IJsonObjectConverter[] converters)
        {
            var settings = new JsonSerializerOptions(CamelCaseDefaultSettings);
            foreach (var c in converters.Select(c => new JsonObjectConverter(c) as JsonConverter))
            {
                settings.Converters.Add(c);
            }
            return settings;
        }
    }
}

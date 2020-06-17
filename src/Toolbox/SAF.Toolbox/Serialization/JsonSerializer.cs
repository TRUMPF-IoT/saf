// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;

namespace SAF.Toolbox.Serialization
{
    public static class JsonSerializer
    {
        private static readonly JsonSerializerSettings CamelCaseDefaultSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: false)
            },
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        public static string Serialize(object obj)
            => JsonConvert.SerializeObject(obj, CamelCaseDefaultSettings);

        public static string Serialize(object obj, params IJsonObjectConverter[] converters) 
            => JsonConvert.SerializeObject(obj, DefaultSettingsWithConverters(converters));

        public static T Deserialize<T>(string json)
            => JsonConvert.DeserializeObject<T>(json, CamelCaseDefaultSettings);

        public static object Deserialize(string json, System.Type type)
            => JsonConvert.DeserializeObject(json, type, CamelCaseDefaultSettings);

        public static T Deserialize<T>(string json, params IJsonObjectConverter[] converters) 
            => JsonConvert.DeserializeObject<T>(json, DefaultSettingsWithConverters(converters));

        public static object Deserialize(string json, System.Type type, params IJsonObjectConverter[] converters)
            => JsonConvert.DeserializeObject(json, type, DefaultSettingsWithConverters(converters));

        private static JsonSerializerSettings DefaultSettingsWithConverters(IJsonObjectConverter[] converters)
        {
            var jsonConverters = converters.Select(c => new JsonObjectConverter(c) as JsonConverter).ToArray();

            return new JsonSerializerSettings
            {
                Converters = jsonConverters,
                ContractResolver = CamelCaseDefaultSettings.ContractResolver,
                DateParseHandling = CamelCaseDefaultSettings.DateParseHandling
            };
        }
    }
}

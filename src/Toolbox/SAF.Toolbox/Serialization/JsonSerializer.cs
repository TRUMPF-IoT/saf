// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JsonTransformer = System.Text.Json.JsonSerializer;
using JsonConverter = System.Text.Json.Serialization.JsonConverter<object>;

namespace SAF.Toolbox.Serialization;

public static class JsonSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();
        
    public static string Serialize(object obj)
        => JsonTransformer.Serialize(obj, DefaultOptions);

    public static string Serialize(object obj, params IJsonObjectConverter[] converters)
        => JsonTransformer.Serialize(obj, DefaultOptionsWithConverters(converters));

    public static T? Deserialize<T>(string json)
    {
        if(json == null) throw new ArgumentNullException(nameof(json));
        return string.IsNullOrWhiteSpace(json) ? default : JsonTransformer.Deserialize<T>(json, DefaultOptions);
    }

    public static object? Deserialize(string json, Type type)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return string.IsNullOrWhiteSpace(json) ? default : JsonTransformer.Deserialize(json, type, DefaultOptions);
    }

    public static T? Deserialize<T>(string json, params IJsonObjectConverter[] converters)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonTransformer.Deserialize<T>(json, DefaultOptionsWithConverters(converters));
    }

    public static object? Deserialize(string json, Type type, params IJsonObjectConverter[] converters)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonTransformer.Deserialize(json, type, DefaultOptionsWithConverters(converters));
    }

    private static JsonSerializerOptions DefaultOptionsWithConverters(IJsonObjectConverter[] converters)
    {
        var settings = new JsonSerializerOptions(DefaultOptions);
        foreach (var c in converters.Select(c => new JsonObjectConverter(c) as JsonConverter))
        {
            settings.Converters.Add(c);
        }

        return settings;
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new ObjectToInferredTypesConverter());

        return options;
    }
}
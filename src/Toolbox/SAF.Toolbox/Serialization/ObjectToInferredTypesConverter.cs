// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SAF.Toolbox.Serialization;

internal class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTimeOffset(out var datetime) => datetime,
            JsonTokenType.String => reader.GetString()!,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value.GetType() == typeof(object))
        {
            // don't use ourselves when serializing an object to avoid StackOverflowException
            var copiedOptions = new JsonSerializerOptions(options);
            copiedOptions.Converters.Remove(this);

            System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType(), copiedOptions);
        }
        else
        {
            System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
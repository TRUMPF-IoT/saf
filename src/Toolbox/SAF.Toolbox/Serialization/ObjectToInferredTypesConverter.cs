// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SAF.Toolbox.Serialization;

internal class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch(reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                {
                    if(reader.TryGetInt64(out var l)) return l;
                    return reader.GetDouble();
                }
            case JsonTokenType.String:
                {
                    if(reader.TryGetDateTimeOffset(out var datetime))
                        return datetime;

                    var stringValue = reader.GetString()!;
                    if (DateTimeOffset.TryParse(stringValue, out var datetimeOffset))
                        return datetimeOffset;

                    return stringValue;
                }
            default:
                return JsonDocument.ParseValue(ref reader).RootElement.Clone();
        };
    }

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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Plugins.Abstractions;

public sealed class PluginIdJsonConverter : JsonConverter<PluginId>
{
    public override PluginId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginId(PluginIdentifierJsonConverterHelpers.ReadString(ref reader, nameof(PluginId)));
    }

    public override void Write(Utf8JsonWriter writer, PluginId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class PluginPackageIdJsonConverter : JsonConverter<PluginPackageId>
{
    public override PluginPackageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginPackageId(PluginIdentifierJsonConverterHelpers.ReadString(ref reader, nameof(PluginPackageId)));
    }

    public override void Write(Utf8JsonWriter writer, PluginPackageId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class PluginConnectionIdJsonConverter : JsonConverter<PluginConnectionId>
{
    public override PluginConnectionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginConnectionId(PluginIdentifierJsonConverterHelpers.ReadGuid(ref reader, nameof(PluginConnectionId)));
    }

    public override void Write(Utf8JsonWriter writer, PluginConnectionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class PluginConnectionKeyJsonConverter : JsonConverter<PluginConnectionKey>
{
    public override PluginConnectionKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginConnectionKey(PluginIdentifierJsonConverterHelpers.ReadString(ref reader, nameof(PluginConnectionKey)));
    }

    public override void Write(Utf8JsonWriter writer, PluginConnectionKey value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class PluginRendererKeyJsonConverter : JsonConverter<PluginRendererKey>
{
    public override PluginRendererKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginRendererKey(PluginIdentifierJsonConverterHelpers.ReadString(ref reader, nameof(PluginRendererKey)));
    }

    public override void Write(Utf8JsonWriter writer, PluginRendererKey value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class PluginHostToolRecipeIdJsonConverter : JsonConverter<PluginHostToolRecipeId>
{
    public override PluginHostToolRecipeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PluginHostToolRecipeId(PluginIdentifierJsonConverterHelpers.ReadString(ref reader, nameof(PluginHostToolRecipeId)));
    }

    public override void Write(Utf8JsonWriter writer, PluginHostToolRecipeId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal static class PluginIdentifierJsonConverterHelpers
{
    public static string ReadString(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? throw new JsonException($"{typeName} cannot be null.");
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"{typeName} must be a string or an object with a value property.");
        }

        string? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"{typeName} JSON object is malformed.");
            }

            var propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return value ?? throw new JsonException($"{typeName}.value is required.");
    }

    public static Guid ReadGuid(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var directGuid))
        {
            return directGuid;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"{typeName} must be a GUID string or an object with a value property.");
        }

        Guid? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"{typeName} JSON object is malformed.");
            }

            var propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.TryGetGuid(out var objectGuid))
                {
                    throw new JsonException($"{typeName}.value must be a GUID.");
                }

                value = objectGuid;
            }
            else
            {
                reader.Skip();
            }
        }

        return value ?? throw new JsonException($"{typeName}.value is required.");
    }
}

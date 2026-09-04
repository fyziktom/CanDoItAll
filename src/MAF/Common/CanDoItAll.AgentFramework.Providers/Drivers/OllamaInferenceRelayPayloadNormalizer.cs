using System.Text.Json;

namespace CanDoItAll.AgentFramework.Providers;

internal static class OllamaInferenceRelayPayloadNormalizer
{
    private static readonly HashSet<string> SchemaMapKeywords =
    [
        "$defs",
        "definitions",
        "dependentSchemas",
        "patternProperties",
        "properties"
    ];

    private static readonly HashSet<string> SchemaArrayKeywords =
    [
        "allOf",
        "anyOf",
        "oneOf",
        "prefixItems"
    ];

    private static readonly HashSet<string> SchemaKeywords =
    [
        "contains",
        "else",
        "if",
        "items",
        "not",
        "propertyNames",
        "then",
        "unevaluatedItems"
    ];

    public static ReadOnlyMemory<byte> Normalize(ReadOnlyMemory<byte> payloadUtf8)
    {
        using var document = JsonDocument.Parse(payloadUtf8);
        using var output = new MemoryStream(payloadUtf8.Length);
        var changed = false;
        using (var writer = new Utf8JsonWriter(output))
        {
            WritePayload(writer, document.RootElement, ref changed);
        }

        return changed ? output.ToArray() : payloadUtf8;
    }

    private static void WritePayload(Utf8JsonWriter writer, JsonElement root, ref bool changed)
    {
        writer.WriteStartObject();
        foreach (var property in root.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals("tools") && property.Value.ValueKind == JsonValueKind.Array)
            {
                WriteTools(writer, property.Value, ref changed);
            }
            else if (property.NameEquals("response_format") && property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteResponseFormat(writer, property.Value, ref changed);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteTools(Utf8JsonWriter writer, JsonElement tools, ref bool changed)
    {
        writer.WriteStartArray();
        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
            {
                tool.WriteTo(writer);
                continue;
            }

            writer.WriteStartObject();
            foreach (var toolProperty in tool.EnumerateObject())
            {
                writer.WritePropertyName(toolProperty.Name);
                if (toolProperty.NameEquals("function") && toolProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    WriteFunction(writer, toolProperty.Value, ref changed);
                }
                else
                {
                    toolProperty.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteFunction(Utf8JsonWriter writer, JsonElement function, ref bool changed)
    {
        writer.WriteStartObject();
        foreach (var property in function.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals("parameters"))
            {
                WriteSchema(writer, property.Value, ref changed);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteResponseFormat(Utf8JsonWriter writer, JsonElement responseFormat, ref bool changed)
    {
        writer.WriteStartObject();
        foreach (var property in responseFormat.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals("json_schema") && property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteSchemaContainer(writer, property.Value, ref changed);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteSchemaContainer(Utf8JsonWriter writer, JsonElement container, ref bool changed)
    {
        writer.WriteStartObject();
        foreach (var property in container.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals("schema"))
            {
                WriteSchema(writer, property.Value, ref changed);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteSchema(Utf8JsonWriter writer, JsonElement schema, ref bool changed)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            changed = true;
            writer.WriteStartObject();
            if (schema.ValueKind == JsonValueKind.False)
            {
                writer.WriteStartObject("not");
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            return;
        }

        if (schema.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in schema.EnumerateArray())
            {
                WriteSchema(writer, item, ref changed);
            }

            writer.WriteEndArray();
            return;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            schema.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (var property in schema.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (SchemaMapKeywords.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteSchemaMap(writer, property.Value, ref changed);
            }
            else if (SchemaArrayKeywords.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Array)
            {
                WriteSchema(writer, property.Value, ref changed);
            }
            else if (SchemaKeywords.Contains(property.Name) &&
                     property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.True or JsonValueKind.False)
            {
                WriteSchema(writer, property.Value, ref changed);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteSchemaMap(Utf8JsonWriter writer, JsonElement map, ref bool changed)
    {
        writer.WriteStartObject();
        foreach (var property in map.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            WriteSchema(writer, property.Value, ref changed);
        }

        writer.WriteEndObject();
    }
}

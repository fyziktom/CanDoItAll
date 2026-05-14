using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkflowInputPayloadText
{
    public static string Resolve(string configuredValue, bool fromInput, WorkflowNodeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return fromInput
            ? Extract(input.PayloadJson)
            : configuredValue;
    }

    private static string Extract(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString() ?? string.Empty,
                JsonValueKind.Object => TryReadCommonTextProperty(document.RootElement, out var value)
                    ? value
                    : payload,
                _ => payload
            };
        }
        catch (JsonException)
        {
            return payload;
        }
    }

    private static bool TryReadCommonTextProperty(JsonElement element, out string value)
    {
        foreach (var propertyName in new[] { "content", "text", "markdown", "message", "responseText" })
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}


using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpToolArgumentsParser
{
    public static IReadOnlyDictionary<string, object?> Parse(
        string jsonArguments,
        McpServerKey serverKey)
    {
        if (string.IsNullOrWhiteSpace(jsonArguments))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(jsonArguments) >
                McpPayloadSizeLimit.Default.MaximumBytes)
            {
                throw new JsonException("The MCP tool arguments exceeded the payload limit.");
            }

            using var document = JsonDocument.Parse(
                jsonArguments,
                new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The root value is not an object.");
            }

            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!arguments.TryAdd(property.Name, property.Value.Clone()))
                {
                    throw new JsonException("Duplicate MCP tool argument property.");
                }
            }

            return arguments;
        }
        catch (JsonException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.JsonParse,
                "$.tools.call.arguments",
                $"MCP tool arguments for '{serverKey}' must be a JSON object.",
                "Serialize the tool request as a JSON object before invoking the MCP runtime.");
        }
    }
}

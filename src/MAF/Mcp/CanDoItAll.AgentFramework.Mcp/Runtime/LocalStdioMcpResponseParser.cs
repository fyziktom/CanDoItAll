using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpResponseParser
{
    public static JsonDocument ParseMessage(
        string message,
        McpServerKey serverKey,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        try
        {
            return JsonDocument.Parse(message);
        }
        catch (JsonException exception)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{serverKey}' returned invalid JSON. {exception.Message}",
                "Inspect the MCP server stdio framing and response payload.");
        }
    }

    public static bool IsResponseForRequest(JsonElement root, int requestId)
    {
        if (!root.TryGetProperty("id", out var id))
        {
            return false;
        }

        return id.ValueKind switch
        {
            JsonValueKind.Number =>
                id.TryGetInt32(out var numericId) && numericId == requestId,
            JsonValueKind.String =>
                string.Equals(
                    id.GetString(),
                    requestId.ToString(),
                    StringComparison.Ordinal),
            _ => false
        };
    }

    public static IReadOnlyList<DiscoveredMcpTool> ParseListToolsResponse(
        JsonDocument response)
    {
        if (!response.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tools", out var tools) ||
            tools.ValueKind != JsonValueKind.Array)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.tools",
                "MCP tools/list response did not include a tools array.",
                "Repair the MCP server tools/list response.");
        }

        return tools.EnumerateArray()
            .Select(ParseTool)
            .ToArray();
    }

    public static object ParseToolArguments(string jsonArguments)
    {
        if (string.IsNullOrWhiteSpace(jsonArguments))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(jsonArguments) ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.TemplateValidation,
                "$.arguments",
                $"MCP tool arguments are not valid JSON. {exception.Message}",
                "Pass a JSON object as MCP tool arguments.");
        }
    }

    private static DiscoveredMcpTool ParseTool(JsonElement tool)
    {
        if (!tool.TryGetProperty("name", out var nameElement) ||
            !McpToolName.TryCreate(nameElement.GetString(), out var name))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.tools[].name",
                "MCP tools/list response included an invalid tool name.",
                "Repair the MCP server tools/list response to return valid MCP tool names.");
        }

        var description = tool.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString() ?? string.Empty
            : string.Empty;
        var inputSchema = tool.TryGetProperty("inputSchema", out var inputSchemaElement)
            ? inputSchemaElement.Clone()
            : (JsonElement?)null;
        return new DiscoveredMcpTool(name, description, inputSchema);
    }
}

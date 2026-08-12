using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpResponseParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = 64
    };

    public static void ValidateInitializeResponse(
        JsonDocument response,
        string expectedProtocolVersion)
    {
        var root = response.RootElement;
        if (!HasSupportedJsonRpcVersion(root) ||
            !root.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("protocolVersion", out var protocolVersion) ||
            protocolVersion.ValueKind != JsonValueKind.String ||
            !string.Equals(
                protocolVersion.GetString(),
                expectedProtocolVersion,
                StringComparison.Ordinal) ||
            !result.TryGetProperty("capabilities", out var capabilities) ||
            capabilities.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("serverInfo", out var serverInfo) ||
            serverInfo.ValueKind != JsonValueKind.Object ||
            !HasNonEmptyString(serverInfo, "name") ||
            !HasNonEmptyString(serverInfo, "version"))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.McpHandshake,
                "$.initialize.result",
                "MCP initialize response is malformed or selected an unsupported protocol version.",
                $"Repair the MCP server initialize response and negotiate protocol version '{expectedProtocolVersion}'.");
        }
    }

    public static bool HasSupportedJsonRpcVersion(JsonElement root)
        => root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("jsonrpc", out var jsonRpc) &&
            jsonRpc.ValueKind == JsonValueKind.String &&
            string.Equals(jsonRpc.GetString(), "2.0", StringComparison.Ordinal);

    public static JsonDocument ParseMessage(
        string message,
        McpServerKey serverKey,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        try
        {
            return JsonDocument.Parse(message, DocumentOptions);
        }
        catch (JsonException)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{serverKey}' returned invalid JSON.",
                "Inspect the MCP server stdio framing and response payload.",
                transportFailure: McpTransportFailureKind.InvalidJson);
        }
    }

    public static bool IsResponseForRequest(JsonElement root, int requestId)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

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
        if (response.RootElement.ValueKind != JsonValueKind.Object ||
            !response.RootElement.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Object ||
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

    private static DiscoveredMcpTool ParseTool(JsonElement tool)
    {
        if (tool.ValueKind != JsonValueKind.Object ||
            !tool.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            !McpToolName.TryCreate(nameElement.GetString(), out var name))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.tools[].name",
                "MCP tools/list response included an invalid tool name.",
                "Repair the MCP server tools/list response to return valid MCP tool names.");
        }

        var description = string.Empty;
        if (tool.TryGetProperty("description", out var descriptionElement))
        {
            if (descriptionElement.ValueKind != JsonValueKind.String)
            {
                throw InvalidToolShape("$.tools[].description");
            }

            description = descriptionElement.GetString() ?? string.Empty;
        }

        JsonElement? inputSchema = null;
        if (tool.TryGetProperty("inputSchema", out var inputSchemaElement))
        {
            if (inputSchemaElement.ValueKind != JsonValueKind.Object)
            {
                throw InvalidToolShape("$.tools[].inputSchema");
            }

            inputSchema = inputSchemaElement.Clone();
        }

        return new DiscoveredMcpTool(name, description, inputSchema);
    }

    private static McpSetupException InvalidToolShape(string fieldPath)
        => new(
            CapabilityDiagnosticCategory.McpListTools,
            fieldPath,
            "MCP tools/list response included an invalid tool property shape.",
            "Repair the MCP server tools/list response schema.");

    private static bool HasNonEmptyString(JsonElement owner, string propertyName)
        => owner.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString());
}

using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpToolResultReader
{
    public static string Read(
        CallToolResult result,
        McpServerKey serverKey,
        McpToolName toolName,
        McpPayloadSizeLimit? payloadSizeLimit = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var sizeLimit = payloadSizeLimit ?? McpPayloadSizeLimit.Default;
        sizeLimit.EnsureValid();
        if (result.IsError is true)
        {
            throw InvalidResult(
                serverKey,
                toolName,
                "reported an execution error",
                CapabilityDiagnosticCategory.RuntimeAdapter);
        }

        if (result.StructuredContent is { } structuredContent)
        {
            if (structuredContent.ValueKind != JsonValueKind.Object)
            {
                throw InvalidResult(
                    serverKey,
                    toolName,
                    "returned structured content that is not a JSON object",
                    CapabilityDiagnosticCategory.SchemaValidation);
            }

            var structuredJson = structuredContent.GetRawText();
            EnsureWithinLimit(structuredJson, sizeLimit, serverKey, toolName);
            return structuredJson;
        }

        if (result.Content is not [TextContentBlock { Text: { } text }] ||
            string.IsNullOrWhiteSpace(text))
        {
            throw InvalidResult(
                serverKey,
                toolName,
                "must return structured content or exactly one non-empty text block containing JSON",
                CapabilityDiagnosticCategory.SchemaValidation);
        }

        EnsureWithinLimit(text, sizeLimit, serverKey, toolName);

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw InvalidResult(
                serverKey,
                toolName,
                "returned a text block that is not valid JSON",
                CapabilityDiagnosticCategory.JsonParse);
        }
    }

    private static void EnsureWithinLimit(
        string json,
        McpPayloadSizeLimit sizeLimit,
        McpServerKey serverKey,
        McpToolName toolName)
    {
        if (json.Length <= sizeLimit.MaximumBytes &&
            Encoding.UTF8.GetByteCount(json) <= sizeLimit.MaximumBytes)
        {
            return;
        }

        throw InvalidResult(
            serverKey,
            toolName,
            $"exceeded the host payload limit of {sizeLimit.MaximumBytes} bytes",
            CapabilityDiagnosticCategory.SchemaValidation);
    }

    private static McpSetupException InvalidResult(
        McpServerKey serverKey,
        McpToolName toolName,
        string detail,
        CapabilityDiagnosticCategory category)
    {
        return new McpSetupException(
            category,
            "$.tools.call.result",
            $"MCP tool '{toolName}' on '{serverKey}' {detail}.",
            "Return one protocol-compliant JSON result without embedding credentials or diagnostic secrets.");
    }
}

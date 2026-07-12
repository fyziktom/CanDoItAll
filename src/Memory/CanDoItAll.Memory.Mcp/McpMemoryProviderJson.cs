using System.Text.Json;

namespace CanDoItAll.Memory.Mcp;

internal static class McpMemoryProviderJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

internal static class MemoryWorkflowInputResolver
{
    public static string ResolveQueryText(
        MemoryWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            return settings.Query.Trim();
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(input.PayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                return document.RootElement.GetString()?.Trim() ?? string.Empty;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                (TryGetText(document.RootElement, "query", out var query) ||
                 TryGetText(document.RootElement, "text", out query) ||
                 TryGetText(document.RootElement, "prompt", out query)))
            {
                return query;
            }
        }
        catch (JsonException)
        {
            return input.PayloadJson.Trim();
        }

        return string.Empty;
    }

    public static MemorySourceSnapshotId? TryParseFirstSourceSnapshotId(
        IReadOnlyList<string> sourceSnapshotIds)
    {
        var sourceSnapshotId = sourceSnapshotIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(sourceSnapshotId)
            ? null
            : MemorySourceSnapshotId.Parse(sourceSnapshotId.Trim());
    }

    private static bool TryGetText(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

}

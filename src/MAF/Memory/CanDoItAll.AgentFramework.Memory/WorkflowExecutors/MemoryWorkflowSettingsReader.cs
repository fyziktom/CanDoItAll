using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Memory;

internal sealed record MemoryWorkflowSettingsReadResult(
    MemoryWorkflowExecutorSettings? Settings,
    string? UnsupportedOperation)
{
    public bool IsUnsupported => UnsupportedOperation is not null;
}

internal static class MemoryWorkflowSettingsReader
{
    private static readonly IReadOnlySet<string> LegacyMutationNames = new HashSet<string>(
        ["IngestText", "FeedbackSubmit", "OperationCancel", "EventAcknowledge"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<int> LegacyMutationValues = new HashSet<int> { 1, 2, 4, 5 };

    public static MemoryWorkflowSettingsReadResult Read(string settingsJson)
    {
        using var document = JsonDocument.Parse(settingsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Memory workflow settings must be a JSON object.");
        }

        var operation = FindOperation(document.RootElement);
        if (operation is { } rawOperation && TryDescribeUnsupported(rawOperation, out var description))
        {
            return new MemoryWorkflowSettingsReadResult(null, description);
        }

        return new MemoryWorkflowSettingsReadResult(
            WorkflowExecutorJson.Deserialize<MemoryWorkflowExecutorSettings>(settingsJson),
            UnsupportedOperation: null);
    }

    private static JsonElement? FindOperation(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(MemoryWorkflowExecutorSettings.Operation), StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static bool TryDescribeUnsupported(JsonElement operation, out string description)
    {
        if (operation.ValueKind == JsonValueKind.String)
        {
            var value = operation.GetString() ?? string.Empty;
            if (LegacyMutationNames.Contains(value) ||
                !Enum.TryParse<MemoryWorkflowOperation>(value, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                description = value;
                return true;
            }
        }
        else if (operation.ValueKind == JsonValueKind.Number && operation.TryGetInt32(out var numeric))
        {
            if (LegacyMutationValues.Contains(numeric) || !Enum.IsDefined((MemoryWorkflowOperation)numeric))
            {
                description = numeric.ToString(CultureInfo.InvariantCulture);
                return true;
            }
        }

        description = string.Empty;
        return false;
    }
}

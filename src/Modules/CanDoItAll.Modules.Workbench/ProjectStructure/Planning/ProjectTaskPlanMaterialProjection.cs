using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanMaterialProjection {
    internal static ProjectTaskPlanTaskFact? Project(ProjectTaskPlanSourceRow row) {
        ArgumentNullException.ThrowIfNull(row);
        ValidateSourceBytes(row);
        if (row.IsSystemManaged || !ProjectStructureCanonicalTaskMutationPolicy.IsTask(row.ObjectType, row.ObjectSubtype)) {
            return null;
        }
        try {
            using var document = JsonDocument.Parse(row.MetadataJson, new JsonDocumentOptions { MaxDepth = 32 });
            var family = Member(document.RootElement, "workItem", required: true);
            if (family.ValueKind != JsonValueKind.Object || EnumValue<ProjectWorkItemKind>(Member(family, "workItemKind", true)) != ProjectWorkItemKind.Task) {
                throw new JsonException();
            }
            var description = Member(family, "description", true);
            if (description.ValueKind != JsonValueKind.String) {
                throw new JsonException();
            }
            var execution = new ProjectTaskExecutionSnapshot(
                EnumValue<ProjectTaskExecutionState>(Member(family, "executionState", true)),
                Date(Member(family, "actualStartedAtUtc", false)), Date(Member(family, "actualEndedAtUtc", false)));
            ProjectTaskExecutionStatePolicy.Validate(execution.State, execution.ActualStartedAtUtc, execution.ActualEndedAtUtc);
            return new(row.Id, row.NodeKey, row.ParentNodeKey, row.Title, row.Subtitle, row.Notes,
                row.Status, row.Priority, row.ProgressMode, row.ProgressPercent, row.StartUtc?.ToUniversalTime(),
                row.EndUtc?.ToUniversalTime(), row.DurationSeconds, description.GetString()!,
                Date(Member(family, "dueUtc", false)), execution);
        } catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
    }

    internal static int ValidateSourceBytes(ProjectTaskPlanSourceRow row) {
        var bytes = 0;
        foreach (var text in new[] { row.NodeKey, row.ObjectSubtype, row.Title, row.Subtitle, row.Notes,
                row.Status, row.ProgressMode, row.MetadataJson }) {
            bytes += ProjectTaskPlanBasisLimits.TextBytes(text);
        }
        if (row.ParentNodeKey is not null) {
            bytes += ProjectTaskPlanBasisLimits.TextBytes(row.ParentNodeKey);
        }
        return bytes;
    }

    private static JsonElement Member(JsonElement parent, string name, bool required) {
        if (parent.ValueKind != JsonValueKind.Object) {
            throw new JsonException();
        }
        var found = false;
        var value = default(JsonElement);
        foreach (var property in parent.EnumerateObject()) {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (found) {
                throw new JsonException();
            }
            found = true;
            value = property.Value;
        }
        if (required && !found) {
            throw new JsonException();
        }
        return value;
    }

    private static T EnumValue<T>(JsonElement value) where T : struct, Enum {
        T parsed;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) {
            parsed = (T)Enum.ToObject(typeof(T), number);
        } else if (value.ValueKind == JsonValueKind.String &&
                Enum.GetNames<T>().FirstOrDefault(candidate => string.Equals(candidate, value.GetString(), StringComparison.OrdinalIgnoreCase)) is { } enumName) {
            parsed = Enum.Parse<T>(enumName);
        } else {
            throw new JsonException();
        }
        return Enum.IsDefined(parsed) ? parsed : throw new JsonException();
    }

    private static DateTimeOffset? Date(JsonElement value) {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String || !value.TryGetDateTimeOffset(out var instant) ||
                value.GetString() is not { } text || !(text.EndsWith('Z') ||
                    text.Length >= 6 && (text[^6] is '+' or '-') && text[^3] == ':')) {
            throw new JsonException();
        }
        return instant.ToUniversalTime();
    }
}

internal sealed class ProjectTaskPlanSourceRow {
    public Guid Id { get; set; }
    public string NodeKey { get; set; } = null!;
    public string? ParentNodeKey { get; set; }
    public ProjectObjectType ObjectType { get; set; }
    public bool IsSystemManaged { get; set; }
    public string ObjectSubtype { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Subtitle { get; set; } = null!;
    public string Notes { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Priority { get; set; }
    public string ProgressMode { get; set; } = null!;
    public int ProgressPercent { get; set; }
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string MetadataJson { get; set; } = null!;
}

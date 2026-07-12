using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

[JsonConverter(typeof(JsonStringEnumConverter<MemoryWorkflowOperation>))]
public enum MemoryWorkflowOperation
{
    ContextQuery = 0,
    OperationStatus = 3
}

public sealed record MemoryWorkflowProviderAssignmentSetting
{
    public MemoryProviderAssignmentScope Scope { get; init; }

    public string Key { get; init; } = string.Empty;

    public string ProviderInstanceId { get; init; } = string.Empty;
}

public sealed record MemoryWorkflowExecutorSettings
{
    public MemoryWorkflowOperation Operation { get; init; } = MemoryWorkflowOperation.ContextQuery;

    public string Query { get; init; } = string.Empty;

    public string ProviderInstanceId { get; init; } = string.Empty;

    public string DefaultProviderInstanceId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedProviderInstanceIds { get; init; } = [];

    public IReadOnlyList<string> AllowedCapabilityIds { get; init; } = [];

    public IReadOnlyList<string> DeniedCapabilityIds { get; init; } = [];

    public IReadOnlyList<MemoryWorkflowProviderAssignmentSetting> ProviderAssignments { get; init; } = [];

    public IReadOnlyList<string> SourceSnapshotIds { get; init; } = [];

    public bool AllowAsync { get; init; }

    public Guid OperationId { get; init; }
}

public static class MemoryWorkflowExecutorCompatibility
{
    private static readonly IReadOnlyDictionary<string, WorkflowExecutorId> LegacyExecutorIds =
        new Dictionary<string, WorkflowExecutorId>(StringComparer.OrdinalIgnoreCase)
        {
            ["cognitive-memory.recall"] = WorkflowExecutorIds.Memory,
            ["cognitive-memory.probe"] = WorkflowExecutorIds.Memory,
            ["cognitive-memory.learning-proposal"] = WorkflowExecutorIds.Memory,
            ["cognitive-memory.review-item"] = WorkflowExecutorIds.Memory
        };

    public static bool TryMapLegacyExecutorId(
        WorkflowExecutorId legacyExecutorId,
        out WorkflowExecutorId mappedExecutorId) =>
        LegacyExecutorIds.TryGetValue(legacyExecutorId.Value, out mappedExecutorId);
}

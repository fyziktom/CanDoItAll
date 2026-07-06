using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

[JsonConverter(typeof(JsonStringEnumConverter<MemoryWorkflowOperation>))]
public enum MemoryWorkflowOperation
{
    ContextQuery = 0,
    IngestText = 1,
    FeedbackSubmit = 2,
    OperationStatus = 3,
    OperationCancel = 4,
    EventAcknowledge = 5
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

    public IReadOnlyList<string> AllowedSourceScopes { get; init; } = [];

    public IReadOnlyList<MemoryWorkflowProviderAssignmentSetting> ProviderAssignments { get; init; } = [];

    public IReadOnlyList<string> SourceSnapshotIds { get; init; } = [];

    public bool AllowAsync { get; init; }

    public bool WaitForAsyncCompletion { get; init; } = true;

    public string Title { get; init; } = string.Empty;

    public string ContentText { get; init; } = string.Empty;

    public string SourceCategory { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string ContextPackId { get; init; } = string.Empty;

    public MemoryFeedbackOutcome Outcome { get; init; } = MemoryFeedbackOutcome.Unknown;

    public string Comment { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public decimal? Amount { get; init; }

    public Guid OperationId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid EventId { get; init; }

    public bool Accepted { get; init; } = true;
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
        out WorkflowExecutorId mappedExecutorId)
        => LegacyExecutorIds.TryGetValue(legacyExecutorId.Value, out mappedExecutorId);
}

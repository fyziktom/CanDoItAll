using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum RecallIntent
{
    Unknown = 0,
    Architecture = 1,
    Implementation = 2,
    Procedure = 3,
    DecisionHistory = 4,
    Debugging = 5,
    Testing = 6,
    Deployment = 7,
    CrossProjectAnalogy = 8,
    SourceLookup = 9
}

public sealed record RecallRequest(
    Guid ProjectId,
    string Query,
    RecallIntent Intent,
    MemoryAccessContext AccessContext,
    RecallOptions Options);

public sealed record RecallOptions(
    int CoarseLimit,
    int FocusLimit,
    int DetailLimit,
    bool IncludeSourceSnippets,
    bool IncludeRelations,
    bool IncludeRecallTrace,
    IReadOnlyList<MemoryType> PreferredMemoryTypes,
    IReadOnlyList<string> RequiredScopes,
    IReadOnlyDictionary<string, string> Properties);

public sealed record MemoryAccessContext(
    string? UserId,
    string? AgentId,
    Guid? ProcessRunId,
    Guid? WorkflowRunId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> AllowedScopes,
    string? ModelProvider,
    string? DataExportPolicy);

public sealed record RecallResult(
    Guid TraceId,
    RecallIntent Intent,
    IReadOnlyList<RecallCandidate> Candidates,
    MemoryContextPack ContextPack,
    IReadOnlyList<string> Warnings);

public sealed record RecallCandidate(
    Guid MemoryItemId,
    string Title,
    MemoryType Type,
    double SemanticScore,
    double LexicalScore,
    double GraphScore,
    double ActivationScore,
    double ConfidenceScore,
    double FinalScore,
    string SelectionReason);

public sealed record MemoryContextPack(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Summary,
    IReadOnlyList<MemoryContextSection> Sections,
    IReadOnlyList<Guid> SourceMemoryItemIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryContextSection(
    string SectionType,
    string Title,
    string Content,
    IReadOnlyList<Guid> MemoryItemIds,
    IReadOnlyList<MemorySourceRef> SourceRefs);

public sealed record RecallTrace(
    Guid Id,
    Guid ProjectId,
    string Query,
    RecallIntent Intent,
    IReadOnlyList<RecallTraceStage> Stages,
    Guid? ContextPackId,
    DateTimeOffset CreatedAtUtc);

public sealed record RecallTraceStage(
    string StageName,
    IReadOnlyList<RecallCandidate> Candidates,
    IReadOnlyDictionary<string, string> Parameters,
    DateTimeOffset CompletedAtUtc);

public interface IRecallOrchestrator
{
    Task<RecallResult> RecallAsync(RecallRequest request, CancellationToken cancellationToken = default);
}

public interface IActivationEngine
{
    double CalculateActivation(MemoryItem item, ActivationContext context);
}

public sealed record ActivationContext(
    DateTimeOffset NowUtc,
    RecallIntent Intent,
    IReadOnlyList<string> ActiveTopics,
    IReadOnlyDictionary<string, string> Properties);

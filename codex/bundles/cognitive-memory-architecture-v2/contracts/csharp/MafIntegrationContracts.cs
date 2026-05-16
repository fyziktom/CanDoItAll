using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

/// <summary>
/// Builds memory context for Microsoft Agent Framework agents.
/// </summary>
public interface ICognitiveMemoryContextProvider
{
    Task<AgentMemoryContext> BuildAgentContextAsync(
        AgentMemoryContextRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AgentMemoryContextRequest(
    Guid ProjectId,
    string AgentId,
    string Goal,
    RecallIntent Intent,
    MemoryAccessContext AccessContext,
    int MaxTokens,
    IReadOnlyDictionary<string, string> Properties);

public sealed record AgentMemoryContext(
    Guid RecallTraceId,
    IReadOnlyList<AgentMemoryMessage> Messages,
    IReadOnlyList<AgentToolHint> ToolHints,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AgentMemoryMessage(
    string Role,
    string Content,
    IReadOnlyList<Guid> SourceMemoryItemIds);

public sealed record AgentToolHint(
    string ToolName,
    string Description,
    IReadOnlyDictionary<string, string> Arguments);

/// <summary>
/// Common contract for workflow executors exposed by the Cognitive Memory module.
/// </summary>
public interface IMemoryWorkflowExecutor
{
    string ExecutorKey { get; }

    Task<MemoryWorkflowExecutorResult> ExecuteAsync(
        MemoryWorkflowExecutorRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryWorkflowExecutorRequest(
    Guid ProjectId,
    Guid WorkflowRunId,
    string NodeKey,
    string InputJson,
    MemoryAccessContext AccessContext);

public sealed record MemoryWorkflowExecutorResult(
    bool Success,
    string OutputJson,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> Artifacts);

public static class MemoryWorkflowExecutorKeys
{
    public const string SourceIngest = "memory.source.ingest";
    public const string Recall = "memory.recall";
    public const string ContextBuild = "memory.context.build";
    public const string Consolidate = "memory.consolidate";
    public const string Project = "memory.project";
    public const string Reflect = "memory.reflect";
    public const string ReviewEnqueue = "memory.review.enqueue";
    public const string ProcedureExtract = "memory.procedure.extract";
    public const string QdrantRebuild = "memory.qdrant.rebuild";
    public const string ProbeSessionStart = "memory.probe.session.start";
    public const string ProbeAsk = "memory.probe.ask";
    public const string ProbeGenerateQuestions = "memory.probe.generateQuestions";
    public const string ProbeFeedback = "memory.probe.feedback";
    public const string ProbeRegressionCreate = "memory.probe.regression.create";
    public const string ProbeRegressionRun = "memory.probe.regression.run";
}

public interface IMemoryToolset
{
    Task<RecallResult> MemoryRecallAsync(RecallRequest request, CancellationToken cancellationToken = default);

    Task<MemoryItem?> MemoryOpenItemAsync(Guid memoryItemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRelation>> MemoryGetRelationsAsync(
        MemoryRelationQuery query,
        CancellationToken cancellationToken = default);
}

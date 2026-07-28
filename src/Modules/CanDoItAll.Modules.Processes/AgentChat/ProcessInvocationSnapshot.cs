using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes.AgentChat;

public enum ProcessInvocationSnapshotSurface
{
    Workspace,
    Live
}

[Flags]
public enum ProcessInvocationSnapshotFieldProfile
{
    None = 0,
    Selection = 1 << 0,
    Definition = 1 << 1,
    Runs = 1 << 2,
    SelectedRunDetail = 1 << 3,
    History = 1 << 4,
    ActiveAgents = 1 << 5,
    UsageTelemetry = 1 << 6,
    ProjectionProvenance = 1 << 7
}

public enum ProcessInvocationSnapshotOmission
{
    Diagnostics,
    ResultLineage,
    ArtifactPaths,
    RestrictedReferences,
    ProviderIdentity,
    ProviderPayloads,
    OperatorActions,
    Incidents,
    ManagerMessages,
    MetricSeries,
    ToolUsageDetails,
    DefinitionEditorData,
    UnselectedDefinitions,
    MutableProjectionReferences
}

public readonly record struct ProcessInvocationSnapshotProvenance(
    ProcessWorkspaceProvenanceComponent Component,
    ProcessProjectionComponentState State,
    ProcessProjectionComponentSource Source,
    ProcessProjectionComponentAbsenceReason AbsenceReason,
    ProcessProjectionContentFingerprint? ContentFingerprint,
    ProcessProjectionFreshness? Freshness,
    ProcessRunRecordProjectionRevision? RunRecordRevision);

public sealed class ProcessInvocationSnapshotCoverage
{
    public ProcessInvocationSnapshotCoverage(
        ProcessInvocationSnapshotFieldProfile fieldProfile,
        IReadOnlyList<ProcessInvocationSnapshotOmission> omissions,
        int sourceRunCount,
        int capturedRunCount,
        int sourceEventCount,
        int capturedEventCount,
        int sourceAgentCount,
        int capturedAgentCount,
        int redactedValueCount)
    {
        if (sourceRunCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRunCount));
        }

        if (capturedRunCount < 0 || capturedRunCount > sourceRunCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedRunCount));
        }

        if (sourceEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEventCount));
        }

        if (capturedEventCount < 0 || capturedEventCount > sourceEventCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedEventCount));
        }

        if (sourceAgentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceAgentCount));
        }

        if (capturedAgentCount < 0 || capturedAgentCount > sourceAgentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedAgentCount));
        }

        if (redactedValueCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redactedValueCount));
        }

        FieldProfile = fieldProfile;
        Omissions = omissions?.Distinct().Order().ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(omissions));
        SourceRunCount = sourceRunCount;
        CapturedRunCount = capturedRunCount;
        SourceEventCount = sourceEventCount;
        CapturedEventCount = capturedEventCount;
        SourceAgentCount = sourceAgentCount;
        CapturedAgentCount = capturedAgentCount;
        RedactedValueCount = redactedValueCount;
    }

    public ProcessInvocationSnapshotFieldProfile FieldProfile { get; }

    public ImmutableArray<ProcessInvocationSnapshotOmission> Omissions { get; }

    public int SourceRunCount { get; }

    public int CapturedRunCount { get; }

    public int SourceEventCount { get; }

    public int CapturedEventCount { get; }

    public int SourceAgentCount { get; }

    public int CapturedAgentCount { get; }

    public int RedactedValueCount { get; }

    public bool HasCompleteRuns => SourceRunCount == CapturedRunCount;

    public bool HasCompleteEvents => SourceEventCount == CapturedEventCount;

    public bool HasCompleteAgents => SourceAgentCount == CapturedAgentCount;
}

public readonly record struct ProcessInvocationDefinitionSnapshot(
    string Key,
    string Name,
    ProcessDefinitionCatalogItemStatus Status,
    ProcessDefinitionCatalogScopeKind Scope,
    string Criticality,
    string OperatingMode,
    int CompatibilityIssueCount,
    DateTimeOffset UpdatedAtUtc);

public readonly record struct ProcessInvocationEventSnapshot(
    Guid EventId,
    long GlobalSequence,
    Guid RootRunId,
    Guid RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    string Summary);

public readonly record struct ProcessInvocationCurrentStepSnapshot(
    Guid StepInstanceId,
    string StepKey,
    string StepStatus,
    string RoleKey,
    string RoleDisplayName,
    string ExecutorDisplayName,
    int AttemptNumber,
    bool IsWorking,
    bool IsLeaseExpired,
    DateTimeOffset UpdatedAtUtc,
    string Summary);

public sealed class ProcessInvocationRunSnapshot
{
    public ProcessInvocationRunSnapshot(
        Guid rootRunId,
        Guid runId,
        ProcessProjectedRunStatus status,
        bool isActive,
        DateTimeOffset firstEventAtUtc,
        DateTimeOffset lastEventAtUtc,
        Guid? projectId,
        string projectName,
        string processName,
        bool isSubprocess,
        int executableStepCount,
        int completedStepCount,
        int terminalStepCount,
        string progressLabel,
        ProcessInvocationCurrentStepSnapshot? currentStep,
        IReadOnlyList<ProcessInvocationEventSnapshot> recentEvents)
    {
        if (rootRunId == Guid.Empty)
        {
            throw new ArgumentException("A root run id is required.", nameof(rootRunId));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A run id is required.", nameof(runId));
        }

        RootRunId = rootRunId;
        RunId = runId;
        Status = status;
        IsActive = isActive;
        FirstEventAtUtc = firstEventAtUtc;
        LastEventAtUtc = lastEventAtUtc;
        ProjectId = projectId;
        ProjectName = projectName;
        ProcessName = processName;
        IsSubprocess = isSubprocess;
        ExecutableStepCount = executableStepCount;
        CompletedStepCount = completedStepCount;
        TerminalStepCount = terminalStepCount;
        ProgressLabel = progressLabel;
        CurrentStep = currentStep;
        RecentEvents = recentEvents?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(recentEvents));
    }

    public Guid RootRunId { get; }

    public Guid RunId { get; }

    public ProcessProjectedRunStatus Status { get; }

    public bool IsActive { get; }

    public DateTimeOffset FirstEventAtUtc { get; }

    public DateTimeOffset LastEventAtUtc { get; }

    public Guid? ProjectId { get; }

    public string ProjectName { get; }

    public string ProcessName { get; }

    public bool IsSubprocess { get; }

    public int ExecutableStepCount { get; }

    public int CompletedStepCount { get; }

    public int TerminalStepCount { get; }

    public string ProgressLabel { get; }

    public ProcessInvocationCurrentStepSnapshot? CurrentStep { get; }

    public ImmutableArray<ProcessInvocationEventSnapshot> RecentEvents { get; }
}

public sealed class ProcessInvocationSelectedRunDetailSnapshot
{
    public ProcessInvocationSelectedRunDetailSnapshot(
        Guid rootRunId,
        Guid runId,
        ProcessProjectedRunStatus status,
        DateTimeOffset firstEventAtUtc,
        DateTimeOffset lastEventAtUtc,
        IReadOnlyList<ProcessInvocationEventSnapshot> recentEvents)
    {
        if (rootRunId == Guid.Empty)
        {
            throw new ArgumentException("A root run id is required.", nameof(rootRunId));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A run id is required.", nameof(runId));
        }

        RootRunId = rootRunId;
        RunId = runId;
        Status = status;
        FirstEventAtUtc = firstEventAtUtc;
        LastEventAtUtc = lastEventAtUtc;
        RecentEvents = recentEvents?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(recentEvents));
    }

    public Guid RootRunId { get; }

    public Guid RunId { get; }

    public ProcessProjectedRunStatus Status { get; }

    public DateTimeOffset FirstEventAtUtc { get; }

    public DateTimeOffset LastEventAtUtc { get; }

    public ImmutableArray<ProcessInvocationEventSnapshot> RecentEvents { get; }
}

public readonly record struct ProcessInvocationActiveAgentSnapshot(
    Guid RunId,
    Guid StepInstanceId,
    Guid? ExecutionRunId,
    Guid? AgentId,
    string AgentName,
    string StepKey,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string Status,
    bool IsWorking,
    bool IsLeaseExpired,
    DateTimeOffset UpdatedAtUtc,
    string Summary);

public readonly record struct ProcessInvocationUsageSnapshot(
    int ObservedRunCount,
    int ActiveRunCount,
    int AttentionRunCount,
    int FailedRunCount,
    int EventCount,
    int ManagerEventCount,
    int ToolCallCount,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal EstimatedCost,
    decimal ActualCost);

public sealed class ProcessInvocationSnapshot : IAgentChatContextAttachment
{
    public ProcessInvocationSnapshot(
        ProcessInvocationSnapshotSurface surface,
        string view,
        string route,
        Guid? projectId,
        AgentChatContextAccessState accessState,
        ProcessRuntimeHistoryWindow? historyWindow,
        ProcessProjectedRunStatus? statusFilter,
        Guid? selectedRunId,
        ProcessInvocationDefinitionSnapshot? selectedDefinition,
        IReadOnlyList<ProcessInvocationRunSnapshot> runs,
        ProcessInvocationSelectedRunDetailSnapshot? selectedRunDetail,
        ProcessInvocationEventSnapshot? focusedEvent,
        ProcessInvocationActiveAgentSnapshot? focusedAgent,
        IReadOnlyList<ProcessInvocationActiveAgentSnapshot> activeAgents,
        ProcessInvocationUsageSnapshot? usage,
        string attentionSummary,
        IReadOnlyList<ProcessInvocationSnapshotProvenance> provenance,
        ProcessInvocationSnapshotCoverage coverage,
        DatabaseProfileGeneration databaseProfileGeneration)
    {
        if (!Enum.IsDefined(surface))
        {
            throw new ArgumentOutOfRangeException(nameof(surface));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Optional project id cannot be empty.", nameof(projectId));
        }

        if (!Enum.IsDefined(accessState))
        {
            throw new ArgumentOutOfRangeException(nameof(accessState));
        }

        if (historyWindow.HasValue && !Enum.IsDefined(historyWindow.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(historyWindow));
        }

        if (statusFilter.HasValue && !Enum.IsDefined(statusFilter.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(statusFilter));
        }

        if (selectedRunId == Guid.Empty)
        {
            throw new ArgumentException("Optional selected run id cannot be empty.", nameof(selectedRunId));
        }

        Surface = surface;
        View = view.Trim();
        Route = route.Trim();
        ProjectId = projectId;
        AccessState = accessState;
        HistoryWindow = historyWindow;
        StatusFilter = statusFilter;
        SelectedRunId = selectedRunId;
        SelectedDefinition = selectedDefinition;
        Runs = runs?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(runs));
        SelectedRunDetail = selectedRunDetail;
        FocusedEvent = focusedEvent;
        FocusedAgent = focusedAgent;
        ActiveAgents = activeAgents?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(activeAgents));
        Usage = usage;
        AttentionSummary = attentionSummary;
        Provenance = provenance?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(provenance));
        Coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));
        DatabaseProfileGeneration = databaseProfileGeneration;
    }

    public ProcessInvocationSnapshotSurface Surface { get; }

    public string View { get; }

    public string Route { get; }

    public Guid? ProjectId { get; }

    public AgentChatContextAccessState AccessState { get; }

    public ProcessRuntimeHistoryWindow? HistoryWindow { get; }

    public ProcessProjectedRunStatus? StatusFilter { get; }

    public Guid? SelectedRunId { get; }

    public ProcessInvocationDefinitionSnapshot? SelectedDefinition { get; }

    public ImmutableArray<ProcessInvocationRunSnapshot> Runs { get; }

    public ProcessInvocationSelectedRunDetailSnapshot? SelectedRunDetail { get; }

    public ProcessInvocationEventSnapshot? FocusedEvent { get; }

    public ProcessInvocationActiveAgentSnapshot? FocusedAgent { get; }

    public ImmutableArray<ProcessInvocationActiveAgentSnapshot> ActiveAgents { get; }

    public ProcessInvocationUsageSnapshot? Usage { get; }

    public string AttentionSummary { get; }

    public ImmutableArray<ProcessInvocationSnapshotProvenance> Provenance { get; }

    public ProcessInvocationSnapshotCoverage Coverage { get; }

    public DatabaseProfileGeneration DatabaseProfileGeneration { get; }
}

internal sealed record ProcessInvocationSnapshotCapture(
    ProcessInvocationSnapshot Snapshot,
    AgentChatContextAttachmentDraft AttachmentDraft);

internal sealed record ProcessAgentChatContextPublication(
    AgentChatContextSurface Surface,
    IReadOnlyList<AgentChatContextContributorPublication> ContributorPublications,
    ProcessInvocationSnapshotCapture? SnapshotCapture);

internal static partial class ProcessInvocationSnapshotMapper
{
    public const string AttachmentKindValue = "processes.invocation-snapshot";
    public const int MaximumCapturedRunCount = 32;
    public const int MaximumRecentEventsPerRun = 6;
    public const int MaximumCapturedActiveAgentCount = 32;
    public static readonly TimeSpan FreshnessLifetime = TimeSpan.FromMinutes(5);

    private const string ContributorIdValue = "processes.runtime-snapshot";
    private const int ContributorOrder = 100;
    private const int MaximumPromptRunCount = 2;
    private const int MaximumPromptActiveAgentCount = 1;
    private const int MaximumPromptRecentEventCount = 1;
    private const int MaximumIdentifierLength = 256;
    private const int MaximumLabelLength = 200;
    private const int MaximumSummaryLength = 600;
    private const string ContentFingerprintVersion = "processes-content-v1";
    private const string CoverageFingerprintVersion = "processes-coverage-v1";
    private const string FreshnessFingerprintVersion = "processes-freshness-v1";
    private static readonly JsonSerializerOptions PromptSerializerOptions = CreatePromptSerializerOptions();

    private const ProcessInvocationSnapshotFieldProfile FieldProfile =
        ProcessInvocationSnapshotFieldProfile.Selection |
        ProcessInvocationSnapshotFieldProfile.Definition |
        ProcessInvocationSnapshotFieldProfile.Runs |
        ProcessInvocationSnapshotFieldProfile.SelectedRunDetail |
        ProcessInvocationSnapshotFieldProfile.History |
        ProcessInvocationSnapshotFieldProfile.ActiveAgents |
        ProcessInvocationSnapshotFieldProfile.UsageTelemetry |
        ProcessInvocationSnapshotFieldProfile.ProjectionProvenance;

    private static readonly ImmutableArray<ProcessInvocationSnapshotOmission> Omissions =
    [
        ProcessInvocationSnapshotOmission.Diagnostics,
        ProcessInvocationSnapshotOmission.ResultLineage,
        ProcessInvocationSnapshotOmission.ArtifactPaths,
        ProcessInvocationSnapshotOmission.RestrictedReferences,
        ProcessInvocationSnapshotOmission.ProviderIdentity,
        ProcessInvocationSnapshotOmission.ProviderPayloads,
        ProcessInvocationSnapshotOmission.OperatorActions,
        ProcessInvocationSnapshotOmission.Incidents,
        ProcessInvocationSnapshotOmission.ManagerMessages,
        ProcessInvocationSnapshotOmission.MetricSeries,
        ProcessInvocationSnapshotOmission.ToolUsageDetails,
        ProcessInvocationSnapshotOmission.DefinitionEditorData,
        ProcessInvocationSnapshotOmission.UnselectedDefinitions,
        ProcessInvocationSnapshotOmission.MutableProjectionReferences
    ];

    public static ProcessAgentChatContextPublication BuildWorkspacePublication(
        ProcessWorkspaceAgentChatContext context,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        ProcessAgentChatContextPublication? previousPublication)
    {
        ArgumentNullException.ThrowIfNull(context);
        var surface = ProcessAgentChatContextBuilder.BuildWorkspaceSurface(context);
        ProcessInvocationSnapshotCapture? capture = null;
        if (context.AccessState == AgentChatContextAccessState.Ready &&
            context.Shell is not null &&
            TryResolveFreshUntilUtc(
                context.Shell.Refresh,
                capturedAtUtc,
                out var freshUntilUtc))
        {
            capture = CaptureWorkspace(
                context,
                surface,
                databaseProfileGeneration,
                capturedAtUtc,
                freshUntilUtc);
        }

        return BuildPublication(surface, capture, previousPublication);
    }

    public static ProcessAgentChatContextPublication BuildLivePublication(
        LiveProcessesAgentChatContext context,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        ProcessAgentChatContextPublication? previousPublication)
    {
        ArgumentNullException.ThrowIfNull(context);
        var surface = ProcessAgentChatContextBuilder.BuildLiveSurface(context);
        ProcessInvocationSnapshotCapture? capture = null;
        if (context.AccessState == AgentChatContextAccessState.Ready &&
            context.Shell is not null &&
            TryResolveFreshUntilUtc(
                context.Shell.Refresh,
                capturedAtUtc,
                out var freshUntilUtc))
        {
            capture = CaptureLive(
                context,
                surface,
                databaseProfileGeneration,
                capturedAtUtc,
                freshUntilUtc);
        }

        return BuildPublication(surface, capture, previousPublication);
    }

    public static SnapshotContentFingerprint ComputeContentFingerprint(
        ProcessInvocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var builder = new StringBuilder();
        AppendValue(builder, ContentFingerprintVersion);
        AppendValue(builder, ((int)snapshot.Surface).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, snapshot.View);
        AppendValue(builder, snapshot.Route);
        AppendValue(builder, snapshot.ProjectId?.ToString("D"));
        AppendValue(
            builder,
            snapshot.DatabaseProfileGeneration.Value.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)snapshot.AccessState).ToString(CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            snapshot.HistoryWindow.HasValue
                ? ((int)snapshot.HistoryWindow.Value).ToString(CultureInfo.InvariantCulture)
                : null);
        AppendValue(builder, snapshot.StatusFilter.HasValue
            ? ((int)snapshot.StatusFilter.Value).ToString(CultureInfo.InvariantCulture)
            : null);
        AppendValue(builder, snapshot.SelectedRunId?.ToString("D"));
        AppendDefinition(builder, snapshot.SelectedDefinition);
        foreach (var run in snapshot.Runs)
        {
            AppendRun(builder, run);
        }

        AppendSelectedRunDetail(builder, snapshot.SelectedRunDetail);
        AppendEvent(builder, snapshot.FocusedEvent);
        AppendAgent(builder, snapshot.FocusedAgent);
        foreach (var agent in snapshot.ActiveAgents)
        {
            AppendAgent(builder, agent);
        }

        AppendUsage(builder, snapshot.Usage);
        AppendValue(builder, snapshot.AttentionSummary);
        foreach (var provenance in snapshot.Provenance)
        {
            AppendProvenance(builder, provenance);
        }

        return new SnapshotContentFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    public static SnapshotCoverageFingerprint ComputeCoverageFingerprint(
        ProcessInvocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var coverage = snapshot.Coverage;
        var builder = new StringBuilder();
        AppendValue(builder, CoverageFingerprintVersion);
        AppendValue(builder, ((int)coverage.FieldProfile).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.SourceRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.CapturedRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.SourceEventCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.CapturedEventCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.SourceAgentCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.CapturedAgentCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.RedactedValueCount.ToString(CultureInfo.InvariantCulture));
        foreach (var omission in coverage.Omissions)
        {
            AppendValue(builder, ((int)omission).ToString(CultureInfo.InvariantCulture));
        }

        return new SnapshotCoverageFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    public static SnapshotFreshnessFingerprint ComputeFreshnessFingerprint(
        ProcessInvocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var builder = new StringBuilder();
        AppendValue(builder, FreshnessFingerprintVersion);
        AppendValue(
            builder,
            snapshot.DatabaseProfileGeneration.Value.ToString(CultureInfo.InvariantCulture));
        foreach (var provenance in snapshot.Provenance)
        {
            AppendProvenance(builder, provenance);
        }

        return new SnapshotFreshnessFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    private static ProcessInvocationSnapshotCapture CaptureWorkspace(
        ProcessWorkspaceAgentChatContext context,
        AgentChatContextSurface surface,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset freshUntilUtc)
    {
        var shell = context.Shell
            ?? throw new ArgumentException("A ready process workspace context requires a shell.", nameof(context));
        var selectedRunId = context.FocusedEvent?.RunId.Value ??
                            context.FocusedRun?.RunId.Value ??
                            context.SelectedRunId ??
                            shell.Runtime.SelectedRunId;
        var selectedRun = ResolveRun(shell, context.FocusedRun, selectedRunId);
        var selectedDefinition = shell.DefinitionCatalog.SelectedItem;
        return CaptureCore(
            ProcessInvocationSnapshotSurface.Workspace,
            surface,
            context.ProjectId,
            shell,
            context.AccessState,
            shell.Runtime.HistoryWindow,
            statusFilter: null,
            selectedRunId,
            selectedDefinition,
            selectedRun,
            context.FocusedEvent,
            focusedAgent: null,
            databaseProfileGeneration,
            capturedAtUtc,
            freshUntilUtc);
    }

    private static ProcessInvocationSnapshotCapture CaptureLive(
        LiveProcessesAgentChatContext context,
        AgentChatContextSurface surface,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset freshUntilUtc)
    {
        var shell = context.Shell
            ?? throw new ArgumentException("A ready live-process context requires a shell.", nameof(context));
        var selectedRunId = context.FilesRunId ??
                            context.FocusedAgent?.RunId ??
                            context.FocusedRun?.RunId.Value ??
                            context.SelectedRunId ??
                            shell.Runtime.SelectedRunId;
        var selectedRun = ResolveRun(shell, context.FocusedRun, selectedRunId);
        return CaptureCore(
            ProcessInvocationSnapshotSurface.Live,
            surface,
            context.ProjectId,
            shell,
            context.AccessState,
            context.HistoryWindow,
            context.StatusFilter,
            selectedRunId,
            selectedDefinition: null,
            selectedRun,
            focusedEvent: null,
            context.FocusedAgent,
            databaseProfileGeneration,
            capturedAtUtc,
            freshUntilUtc);
    }

    private static ProcessInvocationSnapshotCapture CaptureCore(
        ProcessInvocationSnapshotSurface snapshotSurface,
        AgentChatContextSurface surface,
        Guid? projectId,
        ProcessWorkspaceShellProjection shell,
        AgentChatContextAccessState accessState,
        ProcessRuntimeHistoryWindow historyWindow,
        ProcessProjectedRunStatus? statusFilter,
        Guid? selectedRunId,
        ProcessDefinitionCatalogItemProjection? selectedDefinition,
        ProcessLiveProcessSnapshot? selectedRun,
        ProcessTimelineEventProjection? focusedEvent,
        ProcessRuntimeActiveAgentProjection? focusedAgent,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset freshUntilUtc)
    {
        var redactedValueCount = 0;
        var provenanceVector = shell.Provenance;
        var hasSelection = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.Selection);
        var hasDefinition = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.DefinitionCatalog);
        var hasRuns = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.LiveRuns);
        var hasSelectedRunDetail = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.SelectedRunDetail);
        var hasHistory = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.HistoryPage);
        var hasActiveAgents = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.ActiveAgents);
        var hasDerivedProjection = HasPresentComponent(
            provenanceVector,
            ProcessWorkspaceProvenanceComponent.DerivedProjection);
        var trustedSelectedRunId = hasSelection ? selectedRunId : null;
        IReadOnlyList<ProcessLiveProcessSnapshot> sourceRuns =
            hasRuns &&
            selectedRun is not null &&
            shell.Runtime.Runs.All(run => run.RunId != selectedRun.RunId)
                ? [.. shell.Runtime.Runs, selectedRun]
                : hasRuns
                    ? shell.Runtime.Runs
                    : [];
        var sourceEventCount = sourceRuns
            .SelectMany(static run => run.RecentEvents.Select(static runtimeEvent => runtimeEvent.EventId.Value))
            .Concat(
                hasSelectedRunDetail
                    ? shell.Runtime.SelectedRun?.RecentEvents
                        .Select(static runtimeEvent => runtimeEvent.EventId.Value) ?? []
                    : [])
            .Concat(hasHistory && focusedEvent is not null ? [focusedEvent.EventId.Value] : [])
            .Distinct()
            .Count();
        var mappedRuns = sourceRuns
            .OrderByDescending(run => run.RunId.Value == trustedSelectedRunId)
            .ThenByDescending(static run => run.LastEventAtUtc)
            .ThenBy(static run => run.RunId.Value)
            .Take(MaximumCapturedRunCount)
            .Select(run => MapRun(run, ref redactedValueCount))
            .ToImmutableArray();
        var selectedRunDetail = hasSelectedRunDetail
            ? MapSelectedRunDetail(
                shell.Runtime.SelectedRun,
                trustedSelectedRunId,
                ref redactedValueCount)
            : null;
        ProcessInvocationEventSnapshot? mappedFocusedEvent = !hasHistory || focusedEvent is null
            ? null
            : MapEvent(focusedEvent, ref redactedValueCount);
        IReadOnlyList<ProcessRuntimeActiveAgentProjection> sourceAgents =
            hasActiveAgents ? shell.Runtime.ActiveAgents : [];
        var mappedAgents = sourceAgents
            .OrderByDescending(agent =>
                focusedAgent is not null &&
                agent.RunId == focusedAgent.RunId &&
                agent.StepInstanceId == focusedAgent.StepInstanceId)
            .ThenBy(static agent => agent.RunId)
            .ThenBy(static agent => agent.StepInstanceId)
            .Take(MaximumCapturedActiveAgentCount)
            .Select(agent => MapAgent(agent, ref redactedValueCount))
            .ToImmutableArray();
        ProcessInvocationActiveAgentSnapshot? mappedFocusedAgent = !hasActiveAgents || focusedAgent is null
            ? null
            : MapAgent(focusedAgent, ref redactedValueCount);
        ProcessInvocationDefinitionSnapshot? definition = !hasDefinition || selectedDefinition is null
            ? null
            : MapDefinition(selectedDefinition, ref redactedValueCount);
        var attentionSummary = hasDerivedProjection
            ? NormalizeSafeText(
                shell.Runtime.AttentionSummary,
                MaximumSummaryLength,
                "No process attention summary is loaded.",
                ref redactedValueCount)
            : string.Empty;
        var provenance = MapProvenance(provenanceVector);
        var capturedEventCount = mappedRuns
            .SelectMany(static run => run.RecentEvents.Select(static runtimeEvent => runtimeEvent.EventId))
            .Concat(
                selectedRunDetail?.RecentEvents
                    .Select(static runtimeEvent => runtimeEvent.EventId) ?? [])
            .Concat(mappedFocusedEvent is null ? [] : [mappedFocusedEvent.Value.EventId])
            .Distinct()
            .Count();
        var coverage = new ProcessInvocationSnapshotCoverage(
            FieldProfile,
            Omissions,
            sourceRuns.Count,
            mappedRuns.Length,
            sourceEventCount,
            capturedEventCount,
            sourceAgents.Count,
            mappedAgents.Length,
            redactedValueCount);
        var snapshot = new ProcessInvocationSnapshot(
            snapshotSurface,
            hasSelection ? surface.Position.View : "unavailable",
            hasSelection ? surface.Position.Route : "/",
            hasSelection ? projectId : null,
            accessState,
            hasHistory ? historyWindow : null,
            hasSelection ? statusFilter : null,
            trustedSelectedRunId,
            definition,
            mappedRuns,
            selectedRunDetail,
            mappedFocusedEvent,
            mappedFocusedAgent,
            mappedAgents,
            hasDerivedProjection ? MapUsage(shell.Runtime.Stats) : null,
            attentionSummary,
            provenance,
            coverage,
            databaseProfileGeneration);
        var contentFingerprint = ComputeContentFingerprint(snapshot);
        var coverageFingerprint = ComputeCoverageFingerprint(snapshot);
        var freshnessFingerprint = ComputeFreshnessFingerprint(snapshot);
        var normalizedCapturedAtUtc = capturedAtUtc.ToUniversalTime();
        var draft = new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind(AttachmentKindValue),
            contentFingerprint,
            coverageFingerprint,
            databaseProfileGeneration,
            freshnessFingerprint,
            normalizedCapturedAtUtc,
            freshUntilUtc.ToUniversalTime(),
            snapshot);
        return new ProcessInvocationSnapshotCapture(snapshot, draft);
    }

    private static bool TryResolveFreshUntilUtc(
        ProcessWorkspaceProjectionRefreshProjection refresh,
        DateTimeOffset capturedAtUtc,
        out DateTimeOffset freshUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        var normalizedCapturedAtUtc = capturedAtUtc.ToUniversalTime();
        var normalizedObservedAtUtc = refresh.ObservedAtUtc.ToUniversalTime();
        freshUntilUtc = default;

        if (refresh.Status != ProcessWorkspaceProjectionStatus.Ready ||
            refresh.ObservedAtUtc == default ||
            refresh.SourceGlobalSequence < 0 ||
            refresh.BacklogEventCount < 0 ||
            normalizedObservedAtUtc > normalizedCapturedAtUtc)
        {
            return false;
        }

        var sourceDeadlineUtc = normalizedObservedAtUtc.Add(FreshnessLifetime);
        if (normalizedCapturedAtUtc >= sourceDeadlineUtc)
        {
            return false;
        }

        freshUntilUtc = sourceDeadlineUtc;
        return true;
    }

    private static ProcessAgentChatContextPublication BuildPublication(
        AgentChatContextSurface surface,
        ProcessInvocationSnapshotCapture? candidateCapture,
        ProcessAgentChatContextPublication? previousPublication)
    {
        var capture = CanReuse(previousPublication?.SnapshotCapture, candidateCapture)
            ? previousPublication!.SnapshotCapture
            : candidateCapture;
        var fragment = new AgentChatContextFragment(
            new AgentChatContextContributorId(ContributorIdValue),
            ContributorOrder,
            BuildFragmentContent(surface, capture?.Snapshot));
        var contributor = capture is null
            ? new AgentChatContextContributorPublication(fragment)
            : new AgentChatContextContributorPublication(
                fragment,
                [capture.AttachmentDraft]);
        return new ProcessAgentChatContextPublication(
            surface,
            [contributor],
            capture);
    }

    private static bool CanReuse(
        ProcessInvocationSnapshotCapture? previous,
        ProcessInvocationSnapshotCapture? candidate)
    {
        if (previous is null || candidate is null)
        {
            return false;
        }

        if (previous.AttachmentDraft.FreshUntilUtc is { } freshUntilUtc &&
            candidate.AttachmentDraft.CapturedAtUtc >= freshUntilUtc)
        {
            return false;
        }

        return previous.AttachmentDraft.ContentFingerprint ==
               candidate.AttachmentDraft.ContentFingerprint &&
               previous.AttachmentDraft.CoverageFingerprint ==
               candidate.AttachmentDraft.CoverageFingerprint &&
               previous.AttachmentDraft.DatabaseProfileGeneration ==
               candidate.AttachmentDraft.DatabaseProfileGeneration &&
               previous.AttachmentDraft.FreshnessFingerprint ==
               candidate.AttachmentDraft.FreshnessFingerprint;
    }

    private static string BuildFragmentContent(
        AgentChatContextSurface surface,
        ProcessInvocationSnapshot? snapshot)
    {
        var fragment = snapshot is null
            ? JsonSerializer.Serialize(
                new UnavailablePromptContext(
                    Schema: "candoitall.processes.runtime-context.v1",
                    Surface: surface.Position.Surface,
                    View: surface.Position.View,
                    Route: surface.Position.Route,
                    AccessState: surface.AccessState),
                PromptSerializerOptions)
            : JsonSerializer.Serialize(
                new ProcessPromptContext(
                    Schema: "candoitall.processes.runtime-context.v1",
                    snapshot.Surface,
                    snapshot.View,
                    snapshot.Route,
                    snapshot.ProjectId,
                    snapshot.DatabaseProfileGeneration,
                    snapshot.HistoryWindow,
                    snapshot.StatusFilter,
                    snapshot.SelectedRunId,
                    snapshot.SelectedDefinition,
                    snapshot.Runs.Take(MaximumPromptRunCount)
                        .Select(ToPromptRun)
                        .ToArray(),
                    snapshot.SelectedRunDetail is null
                        ? null
                        : new PromptSelectedRunDetail(
                            snapshot.SelectedRunDetail.RootRunId,
                            snapshot.SelectedRunDetail.RunId,
                            snapshot.SelectedRunDetail.Status,
                            snapshot.SelectedRunDetail.FirstEventAtUtc,
                            snapshot.SelectedRunDetail.LastEventAtUtc,
                            snapshot.SelectedRunDetail.RecentEvents
                                .Take(MaximumPromptRecentEventCount)
                                .ToArray()),
                    snapshot.FocusedEvent,
                    snapshot.FocusedAgent,
                    snapshot.ActiveAgents.Take(MaximumPromptActiveAgentCount).ToArray(),
                    snapshot.Usage,
                    snapshot.AttentionSummary,
                    snapshot.Provenance,
                    snapshot.Coverage),
                PromptSerializerOptions);
        if (fragment.Length > AgentChatContextFragment.MaximumContentLength)
        {
            throw new InvalidOperationException(
                "The bounded process invocation context exceeded the agent chat fragment limit.");
        }

        return fragment;
    }

    private static PromptRun ToPromptRun(ProcessInvocationRunSnapshot run)
        => new(
            run.RootRunId,
            run.RunId,
            run.Status,
            run.IsActive,
            run.FirstEventAtUtc,
            run.LastEventAtUtc,
            run.ProjectId,
            run.ProjectName,
            run.ProcessName,
            run.IsSubprocess,
            run.ExecutableStepCount,
            run.CompletedStepCount,
            run.TerminalStepCount,
            run.ProgressLabel,
            run.CurrentStep,
            run.RecentEvents.Take(MaximumPromptRecentEventCount).ToArray());

    private static ProcessLiveProcessSnapshot? ResolveRun(
        ProcessWorkspaceShellProjection shell,
        ProcessLiveProcessSnapshot? focusedRun,
        Guid? selectedRunId)
    {
        if (!selectedRunId.HasValue)
        {
            return null;
        }

        return focusedRun?.RunId.Value == selectedRunId
            ? focusedRun
            : shell.Runtime.Runs.FirstOrDefault(run => run.RunId.Value == selectedRunId);
    }

    private static ProcessInvocationDefinitionSnapshot MapDefinition(
        ProcessDefinitionCatalogItemProjection definition,
        ref int redactedValueCount)
        => new(
            NormalizeSafeText(
                definition.Key.Value,
                MaximumIdentifierLength,
                "[definition key omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                definition.Name,
                MaximumLabelLength,
                "Unnamed process definition",
                ref redactedValueCount),
            definition.Status,
            definition.ScopeKind,
            NormalizeSafeText(
                definition.Criticality,
                MaximumLabelLength,
                "Unspecified",
                ref redactedValueCount),
            NormalizeSafeText(
                definition.OperatingMode,
                MaximumLabelLength,
                "Unspecified",
                ref redactedValueCount),
            definition.CompatibilityIssueCount,
            definition.UpdatedAtUtc.ToUniversalTime());

    private static ProcessInvocationRunSnapshot MapRun(
        ProcessLiveProcessSnapshot run,
        ref int redactedValueCount)
    {
        var mappedEventsBuilder =
            ImmutableArray.CreateBuilder<ProcessInvocationEventSnapshot>();
        foreach (var runtimeEvent in run.RecentEvents
                     .OrderByDescending(static runtimeEvent => runtimeEvent.OccurredAtUtc)
                     .ThenByDescending(static runtimeEvent => runtimeEvent.GlobalSequence)
                     .Take(MaximumRecentEventsPerRun))
        {
            mappedEventsBuilder.Add(MapEvent(runtimeEvent, ref redactedValueCount));
        }

        var mappedEvents = mappedEventsBuilder.ToImmutable();
        ProcessInvocationCurrentStepSnapshot? currentStep = run.CurrentStep is null
            ? null
            : MapCurrentStep(run.CurrentStep, ref redactedValueCount);
        return new ProcessInvocationRunSnapshot(
            run.RootRunId.Value,
            run.RunId.Value,
            run.Status,
            run.IsActive,
            run.FirstEventAtUtc.ToUniversalTime(),
            run.LastEventAtUtc.ToUniversalTime(),
            run.ProjectId,
            NormalizeSafeText(
                run.ProjectName,
                MaximumLabelLength,
                string.Empty,
                ref redactedValueCount),
            NormalizeSafeText(
                run.ProcessName,
                MaximumLabelLength,
                "Unnamed process",
                ref redactedValueCount),
            run.IsSubprocess,
            run.ExecutableStepCount,
            run.CompletedStepCount,
            run.TerminalStepCount,
            NormalizeSafeText(
                run.ProgressLabel,
                MaximumLabelLength,
                "Progress unavailable",
                ref redactedValueCount),
            currentStep,
            mappedEvents);
    }

    private static ProcessInvocationSelectedRunDetailSnapshot? MapSelectedRunDetail(
        ProcessRunDetailProjection? detail,
        Guid? selectedRunId,
        ref int redactedValueCount)
    {
        if (detail is null || detail.RunId.Value != selectedRunId)
        {
            return null;
        }

        var eventsBuilder =
            ImmutableArray.CreateBuilder<ProcessInvocationEventSnapshot>();
        foreach (var runtimeEvent in detail.RecentEvents
                     .OrderByDescending(static runtimeEvent => runtimeEvent.OccurredAtUtc)
                     .ThenByDescending(static runtimeEvent => runtimeEvent.GlobalSequence)
                     .Take(MaximumRecentEventsPerRun))
        {
            eventsBuilder.Add(MapEvent(runtimeEvent, ref redactedValueCount));
        }

        var events = eventsBuilder.ToImmutable();
        return new ProcessInvocationSelectedRunDetailSnapshot(
            detail.RootRunId.Value,
            detail.RunId.Value,
            detail.Status,
            detail.FirstEventAtUtc.ToUniversalTime(),
            detail.LastEventAtUtc.ToUniversalTime(),
            events);
    }

    private static ProcessInvocationCurrentStepSnapshot MapCurrentStep(
        ProcessRuntimeCurrentStepProjection step,
        ref int redactedValueCount)
        => new(
            step.StepInstanceId,
            NormalizeSafeText(
                step.StepKey,
                MaximumIdentifierLength,
                "[step key omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                step.StepStatus,
                MaximumLabelLength,
                "Unknown",
                ref redactedValueCount),
            NormalizeSafeText(
                step.RoleKey,
                MaximumIdentifierLength,
                "[role key omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                step.RoleDisplayName,
                MaximumLabelLength,
                "Unassigned role",
                ref redactedValueCount),
            NormalizeSafeText(
                step.ExecutorDisplayName,
                MaximumLabelLength,
                "Unassigned executor",
                ref redactedValueCount),
            step.AttemptNumber,
            step.IsWorking,
            step.IsLeaseExpired,
            step.UpdatedAtUtc.ToUniversalTime(),
            NormalizeSafeText(
                step.Summary,
                MaximumSummaryLength,
                "Current step summary is unavailable.",
                ref redactedValueCount));

    private static ProcessInvocationEventSnapshot MapEvent(
        ProcessLiveRunEventProjection runtimeEvent,
        ref int redactedValueCount)
        => new(
            runtimeEvent.EventId.Value,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootRunId.Value,
            runtimeEvent.RunId.Value,
            NormalizeSafeText(
                runtimeEvent.EventType,
                MaximumLabelLength,
                "Unknown event",
                ref redactedValueCount),
            runtimeEvent.OccurredAtUtc.ToUniversalTime(),
            runtimeEvent.Sensitivity,
            NormalizeSafeText(
                runtimeEvent.Summary,
                MaximumSummaryLength,
                "Event summary unavailable.",
                ref redactedValueCount));

    private static ProcessInvocationEventSnapshot MapEvent(
        ProcessTimelineEventProjection runtimeEvent,
        ref int redactedValueCount)
        => new(
            runtimeEvent.EventId.Value,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootRunId.Value,
            runtimeEvent.RunId.Value,
            NormalizeSafeText(
                runtimeEvent.EventType,
                MaximumLabelLength,
                "Unknown event",
                ref redactedValueCount),
            runtimeEvent.OccurredAtUtc.ToUniversalTime(),
            runtimeEvent.Sensitivity,
            NormalizeSafeText(
                runtimeEvent.Summary,
                MaximumSummaryLength,
                "Event summary unavailable.",
                ref redactedValueCount));

    private static ProcessInvocationActiveAgentSnapshot MapAgent(
        ProcessRuntimeActiveAgentProjection agent,
        ref int redactedValueCount)
        => new(
            agent.RunId,
            agent.StepInstanceId,
            agent.ExecutionRunId,
            agent.AgentId,
            NormalizeSafeText(
                agent.AgentName,
                MaximumLabelLength,
                "Unnamed agent",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.StepKey,
                MaximumIdentifierLength,
                "[step key omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.RoleKey,
                MaximumIdentifierLength,
                "[role key omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.ExecutorKind,
                MaximumLabelLength,
                "Unknown",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.ExecutorId,
                MaximumIdentifierLength,
                "[executor id omitted]",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.ExecutorDisplayName,
                MaximumLabelLength,
                "Unassigned executor",
                ref redactedValueCount),
            NormalizeSafeText(
                agent.Status,
                MaximumLabelLength,
                "Unknown",
                ref redactedValueCount),
            agent.IsWorking,
            agent.IsLeaseExpired,
            agent.UpdatedAtUtc.ToUniversalTime(),
            NormalizeSafeText(
                agent.Summary,
                MaximumSummaryLength,
                "Agent activity summary unavailable.",
                ref redactedValueCount));

    private static ProcessInvocationUsageSnapshot MapUsage(ProcessRuntimeStatsProjection stats)
        => new(
            stats.ObservedRunCount,
            stats.ActiveRunCount,
            stats.AttentionRunCount,
            stats.FailedRunCount,
            stats.EventCount,
            stats.ManagerEventCount,
            stats.ToolCallCount,
            stats.DurationMs,
            stats.InputTokens,
            stats.CachedInputTokens,
            stats.OutputTokens,
            stats.TotalTokens,
            stats.EstimatedCost,
            stats.ActualCost);

    private static bool HasPresentComponent(
        ProcessWorkspaceProvenanceVector vector,
        ProcessWorkspaceProvenanceComponent component)
        => vector.GetComponent(component).State == ProcessProjectionComponentState.Present;

    private static ImmutableArray<ProcessInvocationSnapshotProvenance> MapProvenance(
        ProcessWorkspaceProvenanceVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Enum.GetValues<ProcessWorkspaceProvenanceComponent>()
            .Select(component =>
            {
                var provenance = vector.GetComponent(component);
                return new ProcessInvocationSnapshotProvenance(
                    component,
                    provenance.State,
                    provenance.Source,
                    provenance.AbsenceReason,
                    provenance.ContentFingerprint,
                    provenance.Freshness,
                    provenance.RunRecordRevision);
            })
            .ToImmutableArray();
    }

    private static string NormalizeSafeText(
        string? value,
        int maximumLength,
        string fallback,
        ref int redactedValueCount)
    {
        var original = value ?? string.Empty;
        var redacted = WorkflowExecutorRedaction.RedactText(original);
        redacted = PotentialPathRegex().Replace(redacted, "[PATH OMITTED]");
        var normalized = string.Join(
            ' ',
            redacted.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = fallback;
        }

        if (normalized.Length > maximumLength)
        {
            normalized = string.Concat(normalized.AsSpan(0, maximumLength - 1), "…");
        }

        if (!string.Equals(original.Trim(), normalized, StringComparison.Ordinal))
        {
            redactedValueCount++;
        }

        return normalized;
    }

    private static void AppendDefinition(
        StringBuilder builder,
        ProcessInvocationDefinitionSnapshot? definition)
    {
        if (!definition.HasValue)
        {
            AppendValue(builder, null);
            return;
        }

        var value = definition.Value;
        AppendValue(builder, value.Key);
        AppendValue(builder, value.Name);
        AppendValue(builder, ((int)value.Status).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)value.Scope).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.Criticality);
        AppendValue(builder, value.OperatingMode);
        AppendValue(builder, value.CompatibilityIssueCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void AppendRun(StringBuilder builder, ProcessInvocationRunSnapshot run)
    {
        AppendValue(builder, run.RootRunId.ToString("D"));
        AppendValue(builder, run.RunId.ToString("D"));
        AppendValue(builder, ((int)run.Status).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, run.IsActive ? "1" : "0");
        AppendValue(builder, run.FirstEventAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, run.LastEventAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, run.ProjectId?.ToString("D"));
        AppendValue(builder, run.ProjectName);
        AppendValue(builder, run.ProcessName);
        AppendValue(builder, run.IsSubprocess ? "1" : "0");
        AppendValue(builder, run.ExecutableStepCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, run.CompletedStepCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, run.TerminalStepCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, run.ProgressLabel);
        if (run.CurrentStep.HasValue)
        {
            var step = run.CurrentStep.Value;
            AppendValue(builder, step.StepInstanceId.ToString("D"));
            AppendValue(builder, step.StepKey);
            AppendValue(builder, step.StepStatus);
            AppendValue(builder, step.RoleKey);
            AppendValue(builder, step.RoleDisplayName);
            AppendValue(builder, step.ExecutorDisplayName);
            AppendValue(builder, step.AttemptNumber.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, step.IsWorking ? "1" : "0");
            AppendValue(builder, step.IsLeaseExpired ? "1" : "0");
            AppendValue(builder, step.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendValue(builder, step.Summary);
        }
        else
        {
            AppendValue(builder, null);
        }

        foreach (var runtimeEvent in run.RecentEvents)
        {
            AppendEvent(builder, runtimeEvent);
        }
    }

    private static void AppendSelectedRunDetail(
        StringBuilder builder,
        ProcessInvocationSelectedRunDetailSnapshot? detail)
    {
        if (detail is null)
        {
            AppendValue(builder, null);
            return;
        }

        AppendValue(builder, detail.RootRunId.ToString("D"));
        AppendValue(builder, detail.RunId.ToString("D"));
        AppendValue(builder, ((int)detail.Status).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, detail.FirstEventAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, detail.LastEventAtUtc.ToString("O", CultureInfo.InvariantCulture));
        foreach (var runtimeEvent in detail.RecentEvents)
        {
            AppendEvent(builder, runtimeEvent);
        }
    }

    private static void AppendEvent(
        StringBuilder builder,
        ProcessInvocationEventSnapshot? runtimeEvent)
    {
        if (!runtimeEvent.HasValue)
        {
            AppendValue(builder, null);
            return;
        }

        AppendEvent(builder, runtimeEvent.Value);
    }

    private static void AppendEvent(
        StringBuilder builder,
        ProcessInvocationEventSnapshot runtimeEvent)
    {
        AppendValue(builder, runtimeEvent.EventId.ToString("D"));
        AppendValue(builder, runtimeEvent.GlobalSequence.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, runtimeEvent.RootRunId.ToString("D"));
        AppendValue(builder, runtimeEvent.RunId.ToString("D"));
        AppendValue(builder, runtimeEvent.EventType);
        AppendValue(builder, runtimeEvent.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)runtimeEvent.Sensitivity).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, runtimeEvent.Summary);
    }

    private static void AppendAgent(
        StringBuilder builder,
        ProcessInvocationActiveAgentSnapshot? agent)
    {
        if (!agent.HasValue)
        {
            AppendValue(builder, null);
            return;
        }

        AppendAgent(builder, agent.Value);
    }

    private static void AppendAgent(
        StringBuilder builder,
        ProcessInvocationActiveAgentSnapshot agent)
    {
        AppendValue(builder, agent.RunId.ToString("D"));
        AppendValue(builder, agent.StepInstanceId.ToString("D"));
        AppendValue(builder, agent.ExecutionRunId?.ToString("D"));
        AppendValue(builder, agent.AgentId?.ToString("D"));
        AppendValue(builder, agent.AgentName);
        AppendValue(builder, agent.StepKey);
        AppendValue(builder, agent.RoleKey);
        AppendValue(builder, agent.ExecutorKind);
        AppendValue(builder, agent.ExecutorId);
        AppendValue(builder, agent.ExecutorDisplayName);
        AppendValue(builder, agent.Status);
        AppendValue(builder, agent.IsWorking ? "1" : "0");
        AppendValue(builder, agent.IsLeaseExpired ? "1" : "0");
        AppendValue(builder, agent.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, agent.Summary);
    }

    private static void AppendUsage(
        StringBuilder builder,
        ProcessInvocationUsageSnapshot? usage)
    {
        if (!usage.HasValue)
        {
            AppendValue(builder, null);
            return;
        }

        var value = usage.Value;
        AppendValue(builder, value.ObservedRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.ActiveRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.AttentionRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.FailedRunCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.EventCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.ManagerEventCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.ToolCallCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.DurationMs.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.InputTokens.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.CachedInputTokens.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.OutputTokens.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.TotalTokens.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.EstimatedCost.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, value.ActualCost.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendProvenance(
        StringBuilder builder,
        ProcessInvocationSnapshotProvenance provenance)
    {
        AppendValue(builder, ((int)provenance.Component).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)provenance.State).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)provenance.Source).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, ((int)provenance.AbsenceReason).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, provenance.ContentFingerprint?.Value);
        AppendValue(
            builder,
            provenance.Freshness?.ObservedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.Freshness?.SourceGlobalSequence.ToString(CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.Freshness?.Lag.LatestKnownGlobalSequence.ToString(
                CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.Freshness?.Lag.LastProcessedGlobalSequence.ToString(
                CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.Freshness?.Lag.BacklogEventCount.ToString(
                CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.RunRecordRevision?.SourceGlobalSequence.ToString(
                CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.RunRecordRevision?.SourceRootSequence.ToString(
                CultureInfo.InvariantCulture));
        AppendValue(
            builder,
            provenance.RunRecordRevision?.UpdatedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, string? value)
    {
        var normalized = value ?? string.Empty;
        builder
            .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(normalized)
            .Append('|');
    }

    private static JsonSerializerOptions CreatePromptSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}_])(?:[A-Za-z]:[\\/][^\s,;]+|\\\\[^\s,;]+|/(?:[^/\s]+/)+[^\s,;]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PotentialPathRegex();

    private sealed record UnavailablePromptContext(
        string Schema,
        string Surface,
        string View,
        string Route,
        AgentChatContextAccessState AccessState);

    private sealed record ProcessPromptContext(
        string Schema,
        ProcessInvocationSnapshotSurface Surface,
        string View,
        string Route,
        Guid? ProjectId,
        DatabaseProfileGeneration DatabaseProfileGeneration,
        ProcessRuntimeHistoryWindow? HistoryWindow,
        ProcessProjectedRunStatus? StatusFilter,
        Guid? SelectedRunId,
        ProcessInvocationDefinitionSnapshot? SelectedDefinition,
        IReadOnlyList<PromptRun> Runs,
        PromptSelectedRunDetail? SelectedRunDetail,
        ProcessInvocationEventSnapshot? FocusedEvent,
        ProcessInvocationActiveAgentSnapshot? FocusedAgent,
        IReadOnlyList<ProcessInvocationActiveAgentSnapshot> ActiveAgents,
        ProcessInvocationUsageSnapshot? Usage,
        string AttentionSummary,
        IReadOnlyList<ProcessInvocationSnapshotProvenance> Provenance,
        ProcessInvocationSnapshotCoverage Coverage);

    private sealed record PromptRun(
        Guid RootRunId,
        Guid RunId,
        ProcessProjectedRunStatus Status,
        bool IsActive,
        DateTimeOffset FirstEventAtUtc,
        DateTimeOffset LastEventAtUtc,
        Guid? ProjectId,
        string ProjectName,
        string ProcessName,
        bool IsSubprocess,
        int ExecutableStepCount,
        int CompletedStepCount,
        int TerminalStepCount,
        string ProgressLabel,
        ProcessInvocationCurrentStepSnapshot? CurrentStep,
        IReadOnlyList<ProcessInvocationEventSnapshot> RecentEvents);

    private sealed record PromptSelectedRunDetail(
        Guid RootRunId,
        Guid RunId,
        ProcessProjectedRunStatus Status,
        DateTimeOffset FirstEventAtUtc,
        DateTimeOffset LastEventAtUtc,
        IReadOnlyList<ProcessInvocationEventSnapshot> RecentEvents);
}

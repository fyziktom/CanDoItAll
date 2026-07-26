using System.Buffers.Binary;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessRunRecordSearchQuery(int Take = 50)
{
    public Guid? ProjectId { get; init; }

    public ProcessDefinitionId? DefinitionId { get; init; }

    public ProcessRunId? RootRunId { get; init; }

    public ProcessRunDisposition? Disposition { get; init; }

    public ProcessRunParticipantId? ParticipantId { get; init; }

    public DateTimeOffset? EndedFromUtc { get; init; }

    public DateTimeOffset? EndedBeforeUtc { get; init; }

    public string? Cursor { get; init; }
}

public sealed record ProcessRunRecordSearchResult(
    IReadOnlyList<ProcessRunRecordSummary> Records,
    string? NextCursor);

public sealed record ProcessRunRecordAnalyticsRequest(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc)
{
    public Guid? ProjectId { get; init; }

    public ProcessDefinitionId? DefinitionId { get; init; }

    public ProcessRunId? RootRunId { get; init; }

    public ProcessRunParticipantId? ParticipantId { get; init; }
}

public enum ProcessRunRecordGraphEdgeKind
{
    Dependency
}

public sealed record ProcessRunRecordGraphNode(
    ProcessRunId OwningRunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    string StepKey,
    ProcessRunStepOutcome Outcome,
    int AttemptCount,
    ProcessRunParticipantId? ParticipantId,
    Guid? WorkflowId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long? DurationMilliseconds,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount);

public sealed record ProcessRunRecordGraphEdge(
    ProcessStepInstanceId SourceStepInstanceId,
    ProcessStepInstanceId TargetStepInstanceId,
    ProcessRunRecordGraphEdgeKind Kind);

public sealed record ProcessRunRecordGraph(
    ProcessRunRecordSummary Summary,
    IReadOnlyList<ProcessRunRecordGraphNode> Nodes,
    IReadOnlyList<ProcessRunRecordGraphEdge> Edges,
    IReadOnlyList<ProcessRunId> SubprocessRunIds,
    int TotalNodeCount,
    int StepOffset,
    int StepTake,
    bool HasMoreNodes);

public sealed class ProcessRunRecordQueryService(IProcessRunRecordStore store)
{
    public const int DefaultGraphStepPageSize = 100;
    public const int MaximumGraphStepPageSize = 200;
    private const int MaximumGraphEdges = 4_096;
    private const int MaximumGraphSubprocessRunIds = 200;

    public static readonly TimeSpan MaximumAnalyticsWindow = TimeSpan.FromDays(366);

    public async Task<ProcessRunRecordSearchResult> ListAsync(
        ProcessRunRecordSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateTake(query.Take);
        ValidateProjectId(query.ProjectId);
        ValidateDefinitionId(query.DefinitionId);
        ValidateRunId(query.RootRunId);
        ValidateDisposition(query.Disposition);
        ValidateParticipantId(query.ParticipantId);
        ValidateTimeRange(query.EndedFromUtc, query.EndedBeforeUtc);

        var page = await store.ListAsync(
            new ProcessRunRecordListQuery(query.Take)
            {
                Payload = ProcessRunRecordListPayload.Compact,
                ProjectId = query.ProjectId,
                DefinitionId = query.DefinitionId,
                RootRunId = query.RootRunId,
                Disposition = query.Disposition,
                ParticipantId = query.ParticipantId,
                EndedFromUtc = NormalizeUtc(query.EndedFromUtc),
                EndedBeforeUtc = NormalizeUtc(query.EndedBeforeUtc),
                Cursor = query.Cursor is null
                    ? null
                    : ProcessRunRecordCursorCodec.Decode(query.Cursor),
                IncludeSuperseded = false
            },
            cancellationToken).ConfigureAwait(false);

        return new ProcessRunRecordSearchResult(
            page.Records.Take(query.Take).ToArray(),
            page.NextCursor is null
                ? null
                : ProcessRunRecordCursorCodec.Encode(page.NextCursor));
    }

    public Task<ProcessRunRecord?> GetAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(runId);
        return store.GetAsync(runId, includeSuperseded: false, cancellationToken);
    }

    public async Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
        ProcessRunRecordAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProjectId(request.ProjectId);
        ValidateDefinitionId(request.DefinitionId);
        ValidateRunId(request.RootRunId);
        ValidateParticipantId(request.ParticipantId);

        var fromUtc = NormalizeUtc(request.FromUtc);
        var toUtc = NormalizeUtc(request.ToUtc);
        ValidateTimeRange(fromUtc, toUtc);
        if (toUtc - fromUtc > MaximumAnalyticsWindow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Process run analytics cannot span more than {MaximumAnalyticsWindow.TotalDays:0} days.");
        }

        return await store.ReadAnalyticsAsync(
            new ProcessRunRecordAnalyticsQuery(fromUtc, toUtc)
            {
                ProjectId = request.ProjectId,
                DefinitionId = request.DefinitionId,
                RootRunId = request.RootRunId,
                ParticipantId = request.ParticipantId
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessRunRecordGraph?> GetGraphAsync(
        ProcessRunId runId,
        int stepOffset = 0,
        int stepTake = DefaultGraphStepPageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(runId);
        ValidateStepPage(stepOffset, stepTake);
        var record = await store
            .GetAsync(runId, includeSuperseded: false, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        if (record.Facts is null)
        {
            return new ProcessRunRecordGraph(
                record.Summary,
                [],
                [],
                [],
                TotalNodeCount: 0,
                stepOffset,
                stepTake,
                HasMoreNodes: false);
        }

        var allSteps = record.Facts.Steps
            .DistinctBy(step => step.StepInstanceId)
            .ToArray();
        var steps = allSteps
            .Skip(stepOffset)
            .Take(stepTake)
            .ToArray();
        var nodes = steps
            .Select(step => new ProcessRunRecordGraphNode(
                step.OwningRunId,
                step.StepInstanceId,
                step.StepDefinitionId,
                step.StepKey,
                step.Outcome,
                step.AttemptCount,
                step.ParticipantId,
                step.WorkflowId,
                step.StartedAtUtc,
                step.EndedAtUtc,
                step.DurationMilliseconds,
                step.TotalTokenCount,
                step.EstimatedCost,
                step.ActualCost,
                step.ToolCallCount,
                step.ArtifactCount))
            .ToArray();
        var nodeIds = steps
            .Select(step => step.StepInstanceId)
            .ToHashSet();
        var edges = steps
            .SelectMany(
                step => step.DependencyStepIds.Take(ProcessRunRecordPayloadLimits.MaximumStepDependencyIds),
                (step, dependencyStepId) => new ProcessRunRecordGraphEdge(
                    dependencyStepId,
                    step.StepInstanceId,
                    ProcessRunRecordGraphEdgeKind.Dependency))
            .Where(edge => nodeIds.Contains(edge.SourceStepInstanceId))
            .Distinct()
            .Take(MaximumGraphEdges)
            .OrderBy(edge => edge.TargetStepInstanceId.Value)
            .ThenBy(edge => edge.SourceStepInstanceId.Value)
            .ToArray();

        return new ProcessRunRecordGraph(
            record.Summary,
            nodes,
            edges,
            record.Facts.SubprocessRunIds
                .Take(MaximumGraphSubprocessRunIds)
                .ToArray(),
            allSteps.Length,
            stepOffset,
            stepTake,
            stepOffset + steps.Length < allSteps.Length);
    }

    private static void ValidateStepPage(int stepOffset, int stepTake)
    {
        if (stepOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepOffset),
                stepOffset,
                "Process run graph step offset cannot be negative.");
        }

        if (stepTake is < 1 or > MaximumGraphStepPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepTake),
                stepTake,
                $"Process run graph step page size must be between 1 and {MaximumGraphStepPageSize}.");
        }
    }

    private static void ValidateTake(int take)
    {
        if (take is < 1 or > ProcessRunRecordPayloadLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process run record page size must be between 1 and {ProcessRunRecordPayloadLimits.MaximumPageSize}.");
        }
    }

    private static void ValidateProjectId(Guid? projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project identifier cannot be empty.", nameof(projectId));
        }
    }

    private static void ValidateDefinitionId(ProcessDefinitionId? definitionId)
    {
        if (definitionId is { Value: var value } && value == Guid.Empty)
        {
            throw new ArgumentException("Definition identifier cannot be empty.", nameof(definitionId));
        }
    }

    private static void ValidateRunId(ProcessRunId? runId)
    {
        if (runId is { Value: var value } && value == Guid.Empty)
        {
            throw new ArgumentException("Run identifier cannot be empty.", nameof(runId));
        }
    }

    private static void ValidateRunId(ProcessRunId runId)
    {
        if (runId.Value == Guid.Empty)
        {
            throw new ArgumentException("Run identifier cannot be empty.", nameof(runId));
        }
    }

    private static void ValidateParticipantId(ProcessRunParticipantId? participantId)
    {
        if (participantId is { Value: var value } && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Participant identifier cannot be empty.", nameof(participantId));
        }
    }

    private static void ValidateDisposition(ProcessRunDisposition? disposition)
    {
        if (disposition is not null && !Enum.IsDefined(disposition.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition,
                "Process run disposition is invalid.");
        }
    }

    private static void ValidateTimeRange(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (fromUtc is not null &&
            toUtc is not null &&
            NormalizeUtc(fromUtc.Value) >= NormalizeUtc(toUtc.Value))
        {
            throw new ArgumentException("The process run record 'fromUtc' value must be earlier than 'toUtc'.");
        }
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value)
        => value is null ? null : NormalizeUtc(value.Value);
}

internal static class ProcessRunRecordCursorCodec
{
    private const byte CurrentVersion = 1;
    private const int EncodedPayloadLength = 25;
    private const int MaximumCursorLength = 128;

    public static string Encode(ProcessRunRecordCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if (cursor.RunId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Process run record cursor run identifier cannot be empty.",
                nameof(cursor));
        }

        Span<byte> payload = stackalloc byte[EncodedPayloadLength];
        payload[0] = CurrentVersion;
        BinaryPrimitives.WriteInt64BigEndian(
            payload[1..9],
            cursor.EndedAtUtc.ToUniversalTime().Ticks);
        if (!cursor.RunId.Value.TryWriteBytes(payload[9..]))
        {
            throw new InvalidOperationException("The process run record cursor could not encode its run identifier.");
        }

        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static ProcessRunRecordCursor Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            throw new ArgumentException("Process run record cursor cannot be empty.", nameof(cursor));
        }

        var normalizedCursor = cursor.Trim();
        if (normalizedCursor.Length > MaximumCursorLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cursor),
                normalizedCursor.Length,
                $"Process run record cursor cannot exceed {MaximumCursorLength} characters.");
        }

        byte[] payload;
        try
        {
            var base64 = normalizedCursor
                .Replace('-', '+')
                .Replace('_', '/');
            var remainder = base64.Length % 4;
            if (remainder == 1)
            {
                throw new FormatException("Invalid Base64Url length.");
            }

            if (remainder > 0)
            {
                base64 = base64.PadRight(base64.Length + 4 - remainder, '=');
            }

            payload = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Process run record cursor is invalid.", nameof(cursor), exception);
        }

        if (payload.Length != EncodedPayloadLength || payload[0] != CurrentVersion)
        {
            throw new ArgumentException("Process run record cursor has an unsupported format.", nameof(cursor));
        }

        try
        {
            var endedAtUtc = new DateTimeOffset(
                BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(1, 8)),
                TimeSpan.Zero);
            var runId = new ProcessRunId(new Guid(payload.AsSpan(9, 16)));
            return new ProcessRunRecordCursor(endedAtUtc, runId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Process run record cursor contains invalid values.", nameof(cursor), exception);
        }
    }
}

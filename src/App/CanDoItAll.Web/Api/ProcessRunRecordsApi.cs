using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Web.Api;

internal static class ProcessRunRecordsApi
{
    private const int DefaultPageSize = 50;
    private const int DefaultDetailStepPageSize = 100;
    private const int MaximumDetailStepPageSize = 200;
    private const int DefaultRuntimeEventMinuteBucketPageSize = 200;
    private const int MaximumRuntimeEventMinuteBucketPageSize = 200;
    private const int MaximumDetailReferenceIds = 200;
    private const int MaximumStepExecutionRunIds = 64;
    private const int MaximumSummaryParticipantIds = 32;
    private const int MaximumNarrativePreviewLength = 512;
    private const int MaximumNarrativeTextLength = 2_048;
    private const int MaximumNarrativeItemLength = 512;
    private const int MaximumNarrativeItemsPerSection = 12;
    private static readonly TimeSpan DefaultAnalyticsWindow = TimeSpan.FromDays(30);
    private static readonly ProcessRunEvidenceSource[] IndividualEvidenceSources =
        Enum.GetValues<ProcessRunEvidenceSource>()
            .Where(source =>
                source is not ProcessRunEvidenceSource.None and not ProcessRunEvidenceSource.All)
            .ToArray();

    public static RouteGroupBuilder MapProcessRunRecordsApi(this RouteGroupBuilder processes)
    {
        processes.MapGet("/runs", async (
                Guid? projectId,
                Guid? definitionId,
                Guid? rootRunId,
                string? disposition,
                string? participantId,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                int? take,
                string? cursor,
                ProcessRunRecordQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await queryService
                    .ListAsync(
                        new ProcessRunRecordSearchQuery(take ?? DefaultPageSize)
                        {
                            ProjectId = projectId,
                            DefinitionId = MapDefinitionId(definitionId),
                            RootRunId = MapRunId(rootRunId, nameof(rootRunId)),
                            Disposition = ParseDisposition(disposition),
                            ParticipantId = MapParticipantId(participantId),
                            EndedFromUtc = fromUtc,
                            EndedBeforeUtc = toUtc,
                            Cursor = cursor
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(new ProcessRunRecordListApiResponse(
                    result.Records.Select(MapListItem).ToArray(),
                    result.NextCursor));
            }
            catch (ArgumentException exception)
            {
                return InvalidQuery(exception);
            }
        })
        .WithName("ListProcessRunRecords");

        processes.MapGet("/runs/analytics", async (
                Guid? projectId,
                Guid? definitionId,
                Guid? rootRunId,
                string? participantId,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                ProcessRunRecordQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var effectiveToUtc = NormalizeUtc(toUtc ?? DateTimeOffset.UtcNow);
                var effectiveFromUtc = NormalizeUtc(fromUtc ?? effectiveToUtc.Subtract(DefaultAnalyticsWindow));
                var result = await queryService
                    .ReadAnalyticsAsync(
                        new ProcessRunRecordAnalyticsRequest(effectiveFromUtc, effectiveToUtc)
                        {
                            ProjectId = projectId,
                            DefinitionId = MapDefinitionId(definitionId),
                            RootRunId = MapRunId(rootRunId, nameof(rootRunId)),
                            ParticipantId = MapParticipantId(participantId)
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(MapAnalytics(result, effectiveFromUtc, effectiveToUtc));
            }
            catch (ArgumentException exception)
            {
                return InvalidQuery(exception);
            }
        })
        .WithName("GetProcessRunRecordAnalytics");

        processes.MapGet("/runs/{runId:guid}/summary", async (
                Guid runId,
                int? stepOffset,
                int? stepTake,
                int? runtimeEventMinuteOffset,
                int? runtimeEventMinuteTake,
                ProcessRunRecordQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var stepPage = NormalizeStepPage(stepOffset, stepTake);
                var runtimeEventMinutePage = NormalizeRuntimeEventMinutePage(
                    runtimeEventMinuteOffset,
                    runtimeEventMinuteTake);
                var record = await queryService
                    .GetAsync(MapRequiredRunId(runId), cancellationToken)
                    .ConfigureAwait(false);
                return record is null
                    ? RunRecordNotFound(runId)
                    : Results.Ok(MapRecord(
                        record,
                        stepPage.Offset,
                        stepPage.Take,
                        runtimeEventMinutePage.Offset,
                        runtimeEventMinutePage.Take));
            }
            catch (ArgumentException exception)
            {
                return InvalidQuery(exception);
            }
        })
        .WithName("GetProcessRunRecordSummary");

        processes.MapGet("/runs/{runId:guid}/graph", async (
                Guid runId,
                int? stepOffset,
                int? stepTake,
                ProcessRunRecordQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var page = NormalizeStepPage(stepOffset, stepTake);
                var graph = await queryService
                    .GetGraphAsync(
                        MapRequiredRunId(runId),
                        page.Offset,
                        page.Take,
                        cancellationToken)
                    .ConfigureAwait(false);
                return graph is null
                    ? RunRecordNotFound(runId)
                    : Results.Ok(MapGraph(graph));
            }
            catch (ArgumentException exception)
            {
                return InvalidQuery(exception);
            }
        })
        .WithName("GetProcessRunRecordGraph");

        return processes;
    }

    private static ProcessRunRecordApiView MapRecord(
        ProcessRunRecord record,
        int stepOffset,
        int stepTake,
        int runtimeEventMinuteOffset,
        int runtimeEventMinuteTake)
    {
        return new ProcessRunRecordApiView(
            MapSummary(record.Summary),
            record.Facts is null
                ? null
                : MapFacts(
                    record.Facts,
                    stepOffset,
                    stepTake,
                    runtimeEventMinuteOffset,
                    runtimeEventMinuteTake),
            record.Summary.Narrative is null ? null : MapNarrative(record.Summary.Narrative));
    }

    private static ProcessRunRecordListItemApiView MapListItem(ProcessRunRecordSummary summary)
    {
        return new ProcessRunRecordListItemApiView(
            MapIdentity(summary.Identity),
            summary.Disposition.ToString(),
            summary.Completeness.ToString(),
            summary.FactsStatus.ToString(),
            summary.FactsAttemptCount,
            summary.FactsNextAttemptAtUtc,
            summary.NarrativeStatus.ToString(),
            summary.NarrativeAttemptCount,
            summary.NarrativeNextAttemptAtUtc,
            MapMetrics(summary.Metrics),
            summary.SourceGlobalSequence,
            BoundText(summary.SchemaVersion, 64),
            summary.UpdatedAtUtc);
    }

    private static ProcessRunRecordSummaryApiView MapSummary(ProcessRunRecordSummary summary)
    {
        return new ProcessRunRecordSummaryApiView(
            MapIdentity(summary.Identity),
            summary.Disposition.ToString(),
            summary.LifecycleState.ToString(),
            summary.Completeness.ToString(),
            new ProcessRunRecordEvidenceApiView(
                MapEvidenceSources(summary.AvailableEvidenceSources),
                MapEvidenceSources(summary.MissingEvidenceSources)),
            summary.CompletenessWarnings
                .Take(ProcessRunRecordPayloadLimits.MaximumCompletenessWarnings)
                .Select(warning => warning.ToString())
                .ToArray(),
            summary.FactsStatus.ToString(),
            summary.FactsAttemptCount,
            summary.FactsNextAttemptAtUtc,
            BoundOptionalText(summary.FactsLastErrorClass, 256),
            summary.NarrativeStatus.ToString(),
            summary.NarrativeAttemptCount,
            summary.NarrativeNextAttemptAtUtc,
            BoundOptionalText(summary.NarrativeLastErrorClass, 256),
            MapMetrics(summary.Metrics),
            summary.ParticipantIds
                .Take(MaximumSummaryParticipantIds)
                .Select(participant => participant.Value)
                .ToArray(),
            summary.Narrative is null
                ? null
                : new ProcessRunNarrativePreviewApiView(
                    BoundText(summary.Narrative.Overview, MaximumNarrativePreviewLength),
                    BoundText(summary.Narrative.Outcome, MaximumNarrativePreviewLength),
                    MapNarrativeProvenance(summary.Narrative.Provenance)),
            summary.SourceGlobalSequence,
            summary.SourceRootSequence,
            BoundText(summary.SchemaVersion, 64),
            summary.UpdatedAtUtc);
    }

    private static ProcessRunRecordIdentityApiView MapIdentity(ProcessRunRecordIdentity identity)
    {
        return new ProcessRunRecordIdentityApiView(
            identity.RunId.Value,
            identity.RootRunId.Value,
            identity.ParentRunId?.Value,
            identity.PlanId?.Value,
            identity.DefinitionId?.Value,
            identity.DefinitionVersionId?.Value,
            identity.ProjectId);
    }

    private static ProcessRunRecordMetricsApiView MapMetrics(ProcessRunRecordMetrics metrics)
    {
        return new ProcessRunRecordMetricsApiView(
            metrics.StartedAtUtc,
            metrics.EndedAtUtc,
            metrics.DurationMilliseconds,
            metrics.TotalStepCount,
            metrics.ExecutableStepCount,
            metrics.CompletedStepCount,
            metrics.FailedStepCount,
            metrics.CancelledStepCount,
            metrics.RepetitionCount,
            metrics.ExecutionCount,
            metrics.ReworkCount,
            metrics.IncidentCount,
            metrics.EscalationCount,
            metrics.InputTokenCount,
            metrics.CachedInputTokenCount,
            metrics.OutputTokenCount,
            metrics.ReasoningTokenCount,
            metrics.TotalTokenCount,
            metrics.EstimatedCost,
            metrics.ActualCost,
            metrics.ToolCallCount,
            metrics.ArtifactCount,
            metrics.SubprocessCount);
    }

    private static ProcessRunHardFactsApiView MapFacts(
        ProcessRunHardFacts facts,
        int stepOffset,
        int stepTake,
        int runtimeEventMinuteOffset,
        int runtimeEventMinuteTake)
    {
        var steps = facts.Steps
            .Skip(stepOffset)
            .Take(stepTake)
            .Select(MapStepFact)
            .ToArray();
        var runtimeEventMinuteBuckets = facts.RuntimeEventMinuteBuckets
            .Skip(runtimeEventMinuteOffset)
            .Take(runtimeEventMinuteTake)
            .Select(bucket => new ProcessRunRuntimeEventMinuteBucketApiView(
                bucket.MinuteUtc,
                bucket.EventCount,
                bucket.ManagerEventCount,
                bucket.DurationMilliseconds))
            .ToArray();
        return new ProcessRunHardFactsApiView(
            new ProcessRunStepPageApiView(
                facts.Steps.Count,
                stepOffset,
                stepTake,
                stepOffset + steps.Length < facts.Steps.Count),
            steps,
            facts.ParticipantIds
                .Take(MaximumDetailReferenceIds)
                .Select(participant => participant.Value)
                .ToArray(),
            facts.ParticipantIds.Count,
            facts.WorkflowIds
                .Take(MaximumDetailReferenceIds)
                .ToArray(),
            facts.WorkflowIds.Count,
            facts.SubprocessRunIds
                .Take(MaximumDetailReferenceIds)
                .Select(runId => runId.Value)
                .ToArray(),
            facts.SubprocessRunIds.Count,
            facts.ExecutionRunIds
                .Take(MaximumDetailReferenceIds)
                .ToArray(),
            facts.ExecutionRunIds.Count,
            facts.ArtifactIds
                .Take(MaximumDetailReferenceIds)
                .Select(artifactId => artifactId.Value)
                .ToArray(),
            facts.ArtifactIds.Count,
            facts.TotalRuntimeEventCount,
            facts.ManagerRuntimeEventCount,
            new ProcessRunRuntimeEventMinuteBucketPageApiView(
                facts.RuntimeEventMinuteBuckets.Count,
                runtimeEventMinuteOffset,
                runtimeEventMinuteTake,
                runtimeEventMinuteOffset + runtimeEventMinuteBuckets.Length <
                    facts.RuntimeEventMinuteBuckets.Count),
            runtimeEventMinuteBuckets,
            facts.RuntimeEventCategories
                .Take(ProcessRunRecordPayloadLimits.MaximumRuntimeEventCategories)
                .Select(category => new ProcessRunRuntimeEventCategoryAggregateApiView(
                    category.Category,
                    category.EventCount,
                    category.FirstOccurredAtUtc,
                    category.LastOccurredAtUtc))
                .ToArray());
    }

    private static ProcessRunStepFactApiView MapStepFact(ProcessRunStepFact step)
    {
        return new ProcessRunStepFactApiView(
            step.OwningRunId.Value,
            step.StepInstanceId.Value,
            step.StepDefinitionId.Value,
            BoundText(step.StepKey, ProcessRunRecordPayloadLimits.MaximumStepKeyLength),
            step.Outcome.ToString(),
            step.AttemptCount,
            step.ParticipantId?.Value,
            step.WorkflowId,
            step.DependencyStepIds
                .Take(ProcessRunRecordPayloadLimits.MaximumStepDependencyIds)
                .Select(stepId => stepId.Value)
                .ToArray(),
            step.ExecutionRunIds
                .Take(MaximumStepExecutionRunIds)
                .ToArray(),
            step.StartedAtUtc,
            step.EndedAtUtc,
            step.DurationMilliseconds,
            step.InputTokenCount,
            step.CachedInputTokenCount,
            step.OutputTokenCount,
            step.ReasoningTokenCount,
            step.TotalTokenCount,
            step.EstimatedCost,
            step.ActualCost,
            step.ToolCallCount,
            step.ArtifactCount);
    }

    private static ProcessRunNarrativeApiView MapNarrative(ProcessRunNarrative narrative)
    {
        return new ProcessRunNarrativeApiView(
            BoundText(narrative.Overview, MaximumNarrativeTextLength),
            BoundText(narrative.Outcome, MaximumNarrativeTextLength),
            MapNarrativeItems(narrative.WorkCompleted),
            MapNarrativeItems(narrative.Problems),
            MapNarrativeItems(narrative.Decisions),
            MapNarrativeItems(narrative.FollowUps),
            MapNarrativeProvenance(narrative.Provenance));
    }

    private static ProcessRunNarrativeProvenanceApiView MapNarrativeProvenance(
        ProcessRunNarrativeProvenance provenance)
    {
        return new ProcessRunNarrativeProvenanceApiView(
            provenance.ManagerAgentId.Value,
            provenance.NarrativeExecutionRunId,
            BoundText(provenance.GenerationPolicyId, 256),
            BoundText(provenance.ModelId, 256),
            provenance.GeneratedAtUtc);
    }

    private static IReadOnlyList<string> MapNarrativeItems(IReadOnlyList<string> items)
    {
        return items
            .Take(MaximumNarrativeItemsPerSection)
            .Select(item => BoundText(item, MaximumNarrativeItemLength))
            .ToArray();
    }

    private static ProcessRunRecordGraphApiView MapGraph(ProcessRunRecordGraph graph)
    {
        return new ProcessRunRecordGraphApiView(
            MapSummary(graph.Summary),
            graph.Nodes.Select(node => new ProcessRunRecordGraphNodeApiView(
                    node.OwningRunId.Value,
                    node.StepInstanceId.Value,
                    node.StepDefinitionId.Value,
                    BoundText(node.StepKey, ProcessRunRecordPayloadLimits.MaximumStepKeyLength),
                    node.Outcome.ToString(),
                    node.AttemptCount,
                    node.ParticipantId?.Value,
                    node.WorkflowId,
                    node.StartedAtUtc,
                    node.EndedAtUtc,
                    node.DurationMilliseconds,
                    node.TotalTokenCount,
                    node.EstimatedCost,
                    node.ActualCost,
                    node.ToolCallCount,
                    node.ArtifactCount))
                .ToArray(),
            graph.Edges.Select(edge => new ProcessRunRecordGraphEdgeApiView(
                    edge.SourceStepInstanceId.Value,
                    edge.TargetStepInstanceId.Value,
                    edge.Kind.ToString()))
                .ToArray(),
            graph.SubprocessRunIds.Select(runId => runId.Value).ToArray(),
            new ProcessRunStepPageApiView(
                graph.TotalNodeCount,
                graph.StepOffset,
                graph.StepTake,
                graph.HasMoreNodes));
    }

    private static ProcessRunRecordAnalyticsApiView MapAnalytics(
        ProcessRunRecordAnalytics analytics,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        return new ProcessRunRecordAnalyticsApiView(
            fromUtc,
            toUtc,
            ProcessRunRecordSchema.CurrentVersion,
            analytics.MatchingRunCount,
            analytics.FactsAvailableRunCount,
            analytics.EvidenceCompleteRunCount,
            analytics.EvidencePartialRunCount,
            analytics.FactsUnavailableRunCount,
            analytics.LatestEndedAtUtc,
            analytics.MaximumSourceGlobalSequence,
            analytics.DurationMilliseconds,
            analytics.InputTokenCount,
            analytics.CachedInputTokenCount,
            analytics.OutputTokenCount,
            analytics.ReasoningTokenCount,
            analytics.TotalTokenCount,
            analytics.EstimatedCost,
            analytics.ActualCost,
            analytics.RepetitionCount,
            analytics.ExecutionCount,
            analytics.ReworkCount,
            analytics.IncidentCount,
            analytics.EscalationCount,
            analytics.ToolCallCount,
            analytics.ArtifactCount,
            analytics.Dispositions
                .Take(Enum.GetValues<ProcessRunDisposition>().Length)
                .Select(disposition => new ProcessRunDispositionAnalyticsApiView(
                    disposition.Disposition.ToString(),
                    disposition.MatchingRunCount))
                .ToArray());
    }

    private static IReadOnlyList<string> MapEvidenceSources(ProcessRunEvidenceSource sources)
    {
        return IndividualEvidenceSources
            .Where(source => (sources & source) == source)
            .Select(source => source.ToString())
            .ToArray();
    }

    private static ProcessDefinitionId? MapDefinitionId(Guid? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value == Guid.Empty)
        {
            throw new ArgumentException("Definition identifier cannot be empty.", nameof(value));
        }

        return new ProcessDefinitionId(value.Value);
    }

    private static ProcessRunId? MapRunId(Guid? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (value == Guid.Empty)
        {
            throw new ArgumentException("Run identifier cannot be empty.", parameterName);
        }

        return new ProcessRunId(value.Value);
    }

    private static ProcessRunId MapRequiredRunId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Run identifier cannot be empty.", nameof(value));
        }

        return new ProcessRunId(value);
    }

    private static ProcessRunDisposition? ParseDisposition(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        foreach (var disposition in Enum.GetValues<ProcessRunDisposition>())
        {
            if (string.Equals(normalized, disposition.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return disposition;
            }
        }

        throw new ArgumentException(
            $"Process run disposition '{BoundText(normalized, 64)}' is invalid.",
            nameof(value));
    }

    private static ProcessRunParticipantId? MapParticipantId(string? value)
        => value is null ? null : new ProcessRunParticipantId(value);

    private static (int Offset, int Take) NormalizeStepPage(int? stepOffset, int? stepTake)
    {
        var offset = stepOffset ?? 0;
        var take = stepTake ?? DefaultDetailStepPageSize;
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepOffset),
                offset,
                "Process run detail step offset cannot be negative.");
        }

        if (take is < 1 or > MaximumDetailStepPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepTake),
                take,
                $"Process run detail step page size must be between 1 and {MaximumDetailStepPageSize}.");
        }

        return (offset, take);
    }

    private static (int Offset, int Take) NormalizeRuntimeEventMinutePage(
        int? runtimeEventMinuteOffset,
        int? runtimeEventMinuteTake)
    {
        var offset = runtimeEventMinuteOffset ?? 0;
        var take = runtimeEventMinuteTake ?? DefaultRuntimeEventMinuteBucketPageSize;
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeEventMinuteOffset),
                offset,
                "Process run runtime-event minute offset cannot be negative.");
        }

        if (take is < 1 or > MaximumRuntimeEventMinuteBucketPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeEventMinuteTake),
                take,
                $"Process run runtime-event minute page size must be between 1 and " +
                $"{MaximumRuntimeEventMinuteBucketPageSize}.");
        }

        return (offset, take);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    private static string BoundText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var bounded = trimmed.Length <= maximumLength
            ? trimmed
            : trimmed[..maximumLength];
        return bounded.ReplaceLineEndings(" ");
    }

    private static string? BoundOptionalText(string? value, int maximumLength)
        => value is null ? null : BoundText(value, maximumLength);

    private static IResult InvalidQuery(ArgumentException exception)
        => ApiEndpointResults.BadRequest(exception.Message, "process.run_record_query_invalid");

    private static IResult RunRecordNotFound(Guid runId)
        => ApiEndpointResults.NotFound(
            $"Process run record '{runId:D}' was not found.",
            "process.run_record_not_found");
}

internal sealed record ProcessRunRecordListApiResponse(
    IReadOnlyList<ProcessRunRecordListItemApiView> Records,
    string? NextCursor);

internal sealed record ProcessRunRecordListItemApiView(
    ProcessRunRecordIdentityApiView Identity,
    string Disposition,
    string Completeness,
    string FactsStatus,
    int FactsAttemptCount,
    DateTimeOffset? FactsNextAttemptAtUtc,
    string NarrativeStatus,
    int NarrativeAttemptCount,
    DateTimeOffset? NarrativeNextAttemptAtUtc,
    ProcessRunRecordMetricsApiView Metrics,
    long SourceGlobalSequence,
    string SchemaVersion,
    DateTimeOffset RecordUpdatedAtUtc);

internal sealed record ProcessRunRecordApiView(
    ProcessRunRecordSummaryApiView Summary,
    ProcessRunHardFactsApiView? Facts,
    ProcessRunNarrativeApiView? Narrative);

internal sealed record ProcessRunRecordSummaryApiView(
    ProcessRunRecordIdentityApiView Identity,
    string Disposition,
    string LifecycleState,
    string Completeness,
    ProcessRunRecordEvidenceApiView Evidence,
    IReadOnlyList<string> CompletenessWarnings,
    string FactsStatus,
    int FactsAttemptCount,
    DateTimeOffset? FactsNextAttemptAtUtc,
    string? FactsLastErrorClass,
    string NarrativeStatus,
    int NarrativeAttemptCount,
    DateTimeOffset? NarrativeNextAttemptAtUtc,
    string? NarrativeLastErrorClass,
    ProcessRunRecordMetricsApiView Metrics,
    IReadOnlyList<string> ParticipantIds,
    ProcessRunNarrativePreviewApiView? NarrativePreview,
    long SourceGlobalSequence,
    long SourceRootSequence,
    string SchemaVersion,
    DateTimeOffset RecordUpdatedAtUtc);

internal sealed record ProcessRunRecordIdentityApiView(
    Guid RunId,
    Guid RootRunId,
    Guid? ParentRunId,
    Guid? PlanId,
    Guid? DefinitionId,
    Guid? DefinitionVersionId,
    Guid? ProjectId);

internal sealed record ProcessRunRecordEvidenceApiView(
    IReadOnlyList<string> Available,
    IReadOnlyList<string> Missing);

internal sealed record ProcessRunRecordMetricsApiView(
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    long? DurationMilliseconds,
    int TotalStepCount,
    int ExecutableStepCount,
    int CompletedStepCount,
    int FailedStepCount,
    int CancelledStepCount,
    int RepetitionCount,
    int ExecutionCount,
    int ReworkCount,
    int IncidentCount,
    int EscalationCount,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount,
    int SubprocessCount);

internal sealed record ProcessRunHardFactsApiView(
    ProcessRunStepPageApiView StepPage,
    IReadOnlyList<ProcessRunStepFactApiView> Steps,
    IReadOnlyList<string> ParticipantIds,
    int ParticipantCount,
    IReadOnlyList<Guid> WorkflowIds,
    int WorkflowCount,
    IReadOnlyList<Guid> SubprocessRunIds,
    int SubprocessRunCount,
    IReadOnlyList<Guid> ExecutionRunIds,
    int ExecutionRunCount,
    IReadOnlyList<Guid> ArtifactIds,
    int ArtifactCount,
    int TotalRuntimeEventCount,
    int ManagerRuntimeEventCount,
    ProcessRunRuntimeEventMinuteBucketPageApiView RuntimeEventMinuteBucketPage,
    IReadOnlyList<ProcessRunRuntimeEventMinuteBucketApiView> RuntimeEventMinuteBuckets,
    IReadOnlyList<ProcessRunRuntimeEventCategoryAggregateApiView> RuntimeEventCategories);

internal sealed record ProcessRunStepPageApiView(
    int TotalCount,
    int Offset,
    int Take,
    bool HasMore);

internal sealed record ProcessRunRuntimeEventMinuteBucketPageApiView(
    int TotalCount,
    int Offset,
    int Take,
    bool HasMore);

internal sealed record ProcessRunRuntimeEventMinuteBucketApiView(
    DateTimeOffset MinuteUtc,
    int EventCount,
    int ManagerEventCount,
    long DurationMilliseconds);

internal sealed record ProcessRunRuntimeEventCategoryAggregateApiView(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ProcessRunRuntimeEventCategory>))]
    ProcessRunRuntimeEventCategory Category,
    int EventCount,
    DateTimeOffset FirstOccurredAtUtc,
    DateTimeOffset LastOccurredAtUtc);

internal sealed record ProcessRunStepFactApiView(
    Guid OwningRunId,
    Guid StepInstanceId,
    Guid StepDefinitionId,
    string StepKey,
    string Outcome,
    int AttemptCount,
    string? ParticipantId,
    Guid? WorkflowId,
    IReadOnlyList<Guid> DependencyStepIds,
    IReadOnlyList<Guid> ExecutionRunIds,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long? DurationMilliseconds,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount);

internal sealed record ProcessRunNarrativePreviewApiView(
    string Overview,
    string Outcome,
    ProcessRunNarrativeProvenanceApiView Provenance);

internal sealed record ProcessRunNarrativeApiView(
    string Overview,
    string Outcome,
    IReadOnlyList<string> WorkCompleted,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> FollowUps,
    ProcessRunNarrativeProvenanceApiView Provenance);

internal sealed record ProcessRunNarrativeProvenanceApiView(
    string ManagerAgentId,
    Guid NarrativeExecutionRunId,
    string GenerationPolicyId,
    string ModelId,
    DateTimeOffset GeneratedAtUtc);

internal sealed record ProcessRunRecordGraphApiView(
    ProcessRunRecordSummaryApiView Summary,
    IReadOnlyList<ProcessRunRecordGraphNodeApiView> Nodes,
    IReadOnlyList<ProcessRunRecordGraphEdgeApiView> Edges,
    IReadOnlyList<Guid> SubprocessRunIds,
    ProcessRunStepPageApiView NodePage);

internal sealed record ProcessRunRecordGraphNodeApiView(
    Guid OwningRunId,
    Guid StepInstanceId,
    Guid StepDefinitionId,
    string StepKey,
    string Outcome,
    int AttemptCount,
    string? ParticipantId,
    Guid? WorkflowId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long? DurationMilliseconds,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount);

internal sealed record ProcessRunRecordGraphEdgeApiView(
    Guid SourceStepInstanceId,
    Guid TargetStepInstanceId,
    string Kind);

internal sealed record ProcessRunRecordAnalyticsApiView(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string SchemaVersion,
    int MatchingRunCount,
    int FactsAvailableRunCount,
    int EvidenceCompleteRunCount,
    int EvidencePartialRunCount,
    int FactsUnavailableRunCount,
    DateTimeOffset? DataThroughUtc,
    long? SourceGlobalSequenceWatermark,
    long DurationMilliseconds,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int RepetitionCount,
    int ExecutionCount,
    int ReworkCount,
    int IncidentCount,
    int EscalationCount,
    int ToolCallCount,
    int ArtifactCount,
    IReadOnlyList<ProcessRunDispositionAnalyticsApiView> Dispositions);

internal sealed record ProcessRunDispositionAnalyticsApiView(
    string Disposition,
    int MatchingRunCount);

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
        processes.MapGet("/runs", ListRunRecordsAsync)
            .WithName("ListProcessRunRecords")
            .Produces<ProcessRunRecordListApiResponse>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        processes.MapGet("/runs/analytics", GetRunRecordAnalyticsAsync)
            .WithName("GetProcessRunRecordAnalytics")
            .Produces<ProcessRunRecordAnalyticsApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        processes.MapGet("/runs/{runId:guid}/summary", GetRunRecordSummaryAsync)
            .WithName("GetProcessRunRecordSummary")
            .Produces<ProcessRunRecordApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        processes.MapGet("/runs/{runId:guid}/graph", GetRunRecordGraphAsync)
            .WithName("GetProcessRunRecordGraph")
            .Produces<ProcessRunRecordGraphApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        return processes;
    }

    /// <summary>
    /// Search the durable records of ended process runs, newest end first, one cursor page at a time.
    /// </summary>
    /// <remarks>
    /// A run record is created when a run ends (it completes, fails, is cancelled or becomes blocked) and is then
    /// completed in the background: first the hard facts (steps, usage, costs, evidence), then a generated narrative.
    /// Active runs have no record yet; use <c>GET /api/processes/live</c> or <c>GET /api/processes/runs/{runId}</c>
    /// for them. Records of child runs (subprocesses) are listed as well, and a record's totals include the child
    /// runs of its run. A run that is reactivated, for example by step rework, has no record until it ends again.
    ///
    /// Records are ordered by <c>metrics.endedAtUtc</c> descending and then by run identifier descending. To page,
    /// send the returned <c>nextCursor</c> as <c>cursor</c> with the same filters until <c>nextCursor</c> is null. The
    /// cursor is opaque and does not carry the filters, so always repeat them.
    ///
    /// Filters are combined with AND. <c>projectId</c>, <c>definitionId</c> and <c>participantId</c> become known
    /// only when a record's facts are assembled (<c>factsStatus</c> Completed); records without facts never match
    /// them. <c>rootRunId</c>, <c>disposition</c>, <c>fromUtc</c> and <c>toUtc</c> apply as soon as a record exists.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host, and every record is
    /// visible to that caller; otherwise the route is open.
    /// </remarks>
    /// <param name="projectId">
    /// Only records of runs launched for this project. The all-zero GUID is rejected.
    /// </param>
    /// <param name="definitionId">
    /// Only records of runs of this process definition (<c>definitionId</c> of the launch response). The all-zero GUID
    /// is rejected.
    /// </param>
    /// <param name="rootRunId">
    /// Only records whose top-level run is this run: the run's own record and the records of its child runs. The
    /// all-zero GUID is rejected.
    /// </param>
    /// <param name="disposition">
    /// Only records that ended this way: <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c> or <c>Blocked</c>, matched
    /// case-insensitively; numbers are not accepted. <c>Escalated</c> is also accepted but is not currently produced.
    /// </param>
    /// <param name="participantId">
    /// Only records whose run involved this participant: an executor identifier of a step assignment or the
    /// identifier of an agent that executed a step (agent identifiers are GUID strings). Exact match after trimming;
    /// 1 to 256 characters.
    /// </param>
    /// <param name="fromUtc">
    /// Only runs that ended at or after this instant (inclusive). When both bounds are given, it must be earlier than
    /// <c>toUtc</c>.
    /// </param>
    /// <param name="toUtc">Only runs that ended before this instant (exclusive).</param>
    /// <param name="take">Page size, from 1 through 200. Omitted means 50.</param>
    /// <param name="cursor">
    /// <c>nextCursor</c> of the previous page, unchanged; omit it for the first page.
    /// </param>
    /// <response code="200">
    /// One page of records, newest end first. An empty <c>records</c> array means that nothing matched.
    /// </response>
    /// <response code="400">
    /// A filter is invalid (<c>process.run_record_query_invalid</c>): <c>take</c> outside 1 through 200, a malformed
    /// cursor, an unknown <c>disposition</c>, an all-zero GUID, a blank or longer than 256 character
    /// <c>participantId</c>, or <c>fromUtc</c> not earlier than <c>toUtc</c>. A value of the wrong type, such as text
    /// for <c>take</c>, is rejected by the framework with HTTP 400 without this envelope.
    /// </response>
    internal static async Task<IResult> ListRunRecordsAsync(
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
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Aggregate the durable records of process runs that ended within a time window.
    /// </summary>
    /// <remarks>
    /// Counts and sums the run records (see <c>GET /api/processes/runs</c>) whose run ended at or after
    /// <c>fromUtc</c> and before <c>toUtc</c> and that match the optional filters. The window defaults to the 30 days
    /// ending now; either bound can be given alone. <c>fromUtc</c> must be earlier than <c>toUtc</c>, and the window
    /// can be at most 366 days long.
    ///
    /// The record counts cover every matching record. Durations, token counts, costs and the activity counts are
    /// summed only over records whose facts are available (<c>factsAvailableRunCount</c>); compare it with
    /// <c>matchingRunCount</c> before drawing conclusions. Child runs have their own records, which are counted too,
    /// while the totals of a parent's record already include its child runs: sums over a window that contains both
    /// therefore count the child runs' usage more than once, and filtering by <c>rootRunId</c> still returns both the
    /// root record and its children's records. For exact figures of one run tree, read the root run's own record. There is no disposition filter; the breakdown is in
    /// <c>dispositions</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="projectId">
    /// Only records of runs launched for this project; matches only records with assembled facts. The all-zero GUID
    /// is rejected.
    /// </param>
    /// <param name="definitionId">
    /// Only records of runs of this process definition; matches only records with assembled facts. The all-zero GUID
    /// is rejected.
    /// </param>
    /// <param name="rootRunId">
    /// Only records whose top-level run is this run: its own record and those of its child runs. The all-zero GUID is
    /// rejected.
    /// </param>
    /// <param name="participantId">
    /// Only records whose run involved this participant (an executor or agent identifier, see
    /// <c>GET /api/processes/runs</c>); matches only records with assembled facts. 1 to 256 characters.
    /// </param>
    /// <param name="fromUtc">
    /// Inclusive start of the window. Omitted means 30 days before <c>toUtc</c>.
    /// </param>
    /// <param name="toUtc">Exclusive end of the window. Omitted means now.</param>
    /// <response code="200">
    /// The aggregates for the effective window, which is echoed in <c>fromUtc</c> and <c>toUtc</c>.
    /// </response>
    /// <response code="400">
    /// A filter is invalid (<c>process.run_record_query_invalid</c>): <c>fromUtc</c> not earlier than <c>toUtc</c>, a
    /// window longer than 366 days, an all-zero GUID, or a blank or longer than 256 character <c>participantId</c>.
    /// </response>
    internal static async Task<IResult> GetRunRecordAnalyticsAsync(
        Guid? projectId,
        Guid? definitionId,
        Guid? rootRunId,
        string? participantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        ProcessRunRecordQueryService queryService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Read the durable record of one ended process run: summary, a page of hard facts, and the narrative.
    /// </summary>
    /// <remarks>
    /// Returns the record that the server keeps for a run that has ended (completed, failed, cancelled or blocked).
    /// The record is created when the run ends and is completed in the background: hard facts are assembled first and
    /// a narrative is then generated by a process manager agent, each with its own status, attempt count and retry
    /// time in <c>summary</c>. <c>facts</c> is null until <c>summary.factsStatus</c> is Completed, and
    /// <c>narrative</c> is null until <c>summary.narrativeStatus</c> is Completed. A missing or failed narrative does
    /// not invalidate facts that are available; check <c>summary.completeness</c> and <c>summary.evidence</c> before
    /// relying on totals.
    ///
    /// The facts cover the run and every child run it launched (see <c>facts.steps[].owningRunId</c>). Steps and
    /// per-minute event buckets are paged separately with zero-based item offsets: <c>stepOffset</c> and
    /// <c>stepTake</c> (1 through 200, default 100), and <c>runtimeEventMinuteOffset</c> and
    /// <c>runtimeEventMinuteTake</c> (1 through 200, default 200). Each page object reports <c>totalCount</c> and
    /// <c>hasMore</c>. Other identifier lists show at most 200 entries next to their stored count.
    ///
    /// For a run that is still active, use <c>GET /api/processes/runs/{runId}</c>: this record does not exist until
    /// the run ends, and it is withdrawn while a reactivated run runs again. Use <c>/graph</c> for step dependencies.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the ended process run, for example from <c>GET /api/processes/runs</c> or from the launch
    /// response. The all-zero GUID is rejected.
    /// </param>
    /// <param name="stepOffset">Number of steps to skip, zero-based. Omitted means 0; must not be negative.</param>
    /// <param name="stepTake">Number of steps to return, from 1 through 200. Omitted means 100.</param>
    /// <param name="runtimeEventMinuteOffset">
    /// Number of per-minute event buckets to skip, zero-based. Omitted means 0; must not be negative.
    /// </param>
    /// <param name="runtimeEventMinuteTake">
    /// Number of per-minute event buckets to return, from 1 through 200. Omitted means 200.
    /// </param>
    /// <response code="200">The record with the requested pages.</response>
    /// <response code="400">
    /// A paging value is out of range, or the run identifier is the all-zero GUID
    /// (<c>process.run_record_query_invalid</c>). Paging is checked before the record is looked up.
    /// </response>
    /// <response code="404">
    /// There is no current record for this run (<c>process.run_record_not_found</c>): the run does not exist, has not
    /// ended, its end has not been processed yet, or it was reactivated and is running again.
    /// </response>
    internal static async Task<IResult> GetRunRecordSummaryAsync(
        Guid runId,
        int? stepOffset,
        int? stepTake,
        int? runtimeEventMinuteOffset,
        int? runtimeEventMinuteTake,
        ProcessRunRecordQueryService queryService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Read the step dependency graph of an ended process run, one page of steps at a time.
    /// </summary>
    /// <remarks>
    /// Returns the record summary (as in <c>/summary</c>), one page of steps as nodes, and the dependency edges
    /// between steps of that page. Nodes include the steps of child runs (see <c>owningRunId</c>) and follow the
    /// stored step order: the run's own steps first, then those of its child runs. An edge from
    /// <c>sourceStepInstanceId</c> to <c>targetStepInstanceId</c> means that the target step depends on the source
    /// step. An edge is returned only when both steps are on the returned page, so request pages that together cover
    /// every step to see every edge. While the record's facts are not assembled, the graph is empty
    /// (<c>nodePage.totalCount</c> 0).
    ///
    /// Paging: <c>stepOffset</c> is a zero-based number of steps to skip (default 0) and <c>stepTake</c> the page
    /// size, from 1 through 200 (default 100).
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">Identifier of the ended process run. The all-zero GUID is rejected.</param>
    /// <param name="stepOffset">Number of steps to skip, zero-based. Omitted means 0; must not be negative.</param>
    /// <param name="stepTake">Number of steps to return, from 1 through 200. Omitted means 100.</param>
    /// <response code="200">The graph page.</response>
    /// <response code="400">
    /// A paging value is out of range, or the run identifier is the all-zero GUID
    /// (<c>process.run_record_query_invalid</c>).
    /// </response>
    /// <response code="404">
    /// There is no current record for this run (<c>process.run_record_not_found</c>): the run does not exist, has not
    /// ended, its end has not been processed yet, or it was reactivated and is running again.
    /// </response>
    internal static async Task<IResult> GetRunRecordGraphAsync(
        Guid runId,
        int? stepOffset,
        int? stepTake,
        ProcessRunRecordQueryService queryService,
        CancellationToken cancellationToken)
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

/// <summary>
/// One page of <c>GET /api/processes/runs</c>.
/// </summary>
/// <param name="Records">
/// Records on this page, ordered by end time (newest first) and then run identifier (descending). Empty when nothing
/// matched.
/// </param>
/// <param name="NextCursor">
/// Opaque cursor of the next page: send it as <c>cursor</c> with the same filters. Null when no further record
/// matched at the time of the query.
/// </param>
internal sealed record ProcessRunRecordListApiResponse(
    IReadOnlyList<ProcessRunRecordListItemApiView> Records,
    string? NextCursor);

/// <summary>
/// Compact durable record of one ended process run in a list page. Read
/// <c>GET /api/processes/runs/{runId}/summary</c> for evidence, warnings, participants and the narrative.
/// </summary>
/// <param name="Identity">Run, plan, definition and project identities of the record.</param>
/// <param name="Disposition">
/// How the run ended, as text: <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c> or <c>Blocked</c>
/// (<c>Escalated</c> is defined but not currently produced).
/// </param>
/// <param name="Completeness">
/// Evidence completeness, as text: <c>SeedOnly</c> (facts not assembled yet), <c>Partial</c> (facts assembled, but
/// evidence is missing or a warning was raised) or <c>Complete</c>.
/// </param>
/// <param name="FactsStatus">
/// Progress of the hard-facts assembly, as text: <c>Pending</c>, <c>Assembling</c>, <c>Completed</c> or
/// <c>Failed</c>.
/// </param>
/// <param name="FactsAttemptCount">Number of times the background worker has started assembling the facts.</param>
/// <param name="FactsNextAttemptAtUtc">
/// When the worker retries after a failed attempt; null when no retry is scheduled, for example while assembling,
/// after success, or when the attempts are used up (5 by default).
/// </param>
/// <param name="NarrativeStatus">
/// Progress of the narrative generation, as text: <c>Pending</c>, <c>Generating</c>, <c>Completed</c> or
/// <c>Failed</c>. The narrative is generated only after the facts are Completed.
/// </param>
/// <param name="NarrativeAttemptCount">
/// Number of times the background worker has started generating the narrative.
/// </param>
/// <param name="NarrativeNextAttemptAtUtc">
/// When the worker retries the narrative; null when no retry is scheduled.
/// </param>
/// <param name="Metrics">Timing, step, usage and cost totals of the run and its child runs.</param>
/// <param name="SourceGlobalSequence">
/// <c>globalSequence</c> of the runtime event that ended the run and created this version of the record. A larger
/// value for the same run means a newer version, for example after a reactivated run ended again.
/// </param>
/// <param name="SchemaVersion">Version of the record format, currently <c>1.0</c>.</param>
/// <param name="RecordUpdatedAtUtc">
/// When the record last changed: created, picked up by the worker, completed or failed.
/// </param>
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

/// <summary>
/// Durable record of one ended process run, returned by <c>GET /api/processes/runs/{runId}/summary</c>.
/// </summary>
/// <param name="Summary">Status, evidence and totals of the record.</param>
/// <param name="Facts">
/// The requested page of hard facts; null until <c>summary.factsStatus</c> is Completed.
/// </param>
/// <param name="Narrative">
/// The generated narrative; null until <c>summary.narrativeStatus</c> is Completed.
/// </param>
internal sealed record ProcessRunRecordApiView(
    ProcessRunRecordSummaryApiView Summary,
    ProcessRunHardFactsApiView? Facts,
    ProcessRunNarrativeApiView? Narrative);

/// <summary>
/// Status, evidence and totals of a durable run record, as returned by the summary and graph operations.
/// </summary>
/// <param name="Identity">Run, plan, definition and project identities of the record.</param>
/// <param name="Disposition">
/// How the run ended, as text: <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c> or <c>Blocked</c>
/// (<c>Escalated</c> is defined but not currently produced).
/// </param>
/// <param name="LifecycleState">
/// Always <c>Current</c> here: only the current version of a record is returned. A version replaced after a
/// reactivation (<c>Superseded</c>) is not returned.
/// </param>
/// <param name="Completeness">
/// Evidence completeness, as text: <c>SeedOnly</c> (facts not assembled yet), <c>Partial</c> (facts assembled, but
/// evidence is missing or a warning was raised) or <c>Complete</c>.
/// </param>
/// <param name="Evidence">Evidence sources that were available and missing when the facts were assembled.</param>
/// <param name="CompletenessWarnings">
/// Codes of the problems found while assembling the facts, at most 64: missing evidence (<c>MissingInstancePlan</c>,
/// <c>MissingStepAssignments</c>, <c>MissingExecutionObservations</c>, <c>MissingUsageTelemetry</c>,
/// <c>MissingPricing</c>, <c>MissingArtifactLineage</c>, <c>MissingRuntimeEvents</c>,
/// <c>MissingSubprocessEvidence</c>, <c>MissingSubprocessParentMetadata</c>), child runs that had not ended or could
/// not be found (<c>SubprocessNonTerminal</c>, <c>SubprocessDepthLimitReached</c>, <c>SubprocessDiscoveryFailed</c>),
/// truncated lists (codes ending in <c>Truncated</c>), inconsistent data (<c>MissingStepTiming</c>,
/// <c>InvalidRunTiming</c>, <c>UnallocatedUsage</c>, <c>MissingStepKey</c>, <c>InvalidProjectId</c>) and
/// <c>PrimaryRunBlocked</c>. Empty when there were none or the facts are not assembled yet.
/// </param>
/// <param name="FactsStatus">
/// Progress of the hard-facts assembly, as text: <c>Pending</c>, <c>Assembling</c>, <c>Completed</c> or
/// <c>Failed</c>.
/// </param>
/// <param name="FactsAttemptCount">Number of times the background worker has started assembling the facts.</param>
/// <param name="FactsNextAttemptAtUtc">
/// When the worker retries after a failed attempt; null when no retry is scheduled, for example while assembling,
/// after success, or when the attempts are used up (5 by default).
/// </param>
/// <param name="FactsLastErrorClass">
/// Short type name of the error of the last failed facts attempt, without its message; null when the last attempt
/// did not fail.
/// </param>
/// <param name="NarrativeStatus">
/// Progress of the narrative generation, as text: <c>Pending</c>, <c>Generating</c>, <c>Completed</c> or
/// <c>Failed</c>. The narrative is generated only after the facts are Completed.
/// </param>
/// <param name="NarrativeAttemptCount">
/// Number of times the background worker has started generating the narrative.
/// </param>
/// <param name="NarrativeNextAttemptAtUtc">
/// When the worker retries the narrative; null when no retry is scheduled.
/// </param>
/// <param name="NarrativeLastErrorClass">
/// Short type name of the error of the last failed or deferred narrative attempt, without its message; null when the
/// last attempt did not fail.
/// </param>
/// <param name="Metrics">Timing, step, usage and cost totals of the run and its child runs.</param>
/// <param name="ParticipantIds">
/// Up to 32 participants of the run and its child runs: executor identifiers of step assignments and identifiers of
/// agents that executed steps (agent identifiers are GUID strings). Empty until the facts are assembled; the facts
/// list up to 200.
/// </param>
/// <param name="NarrativePreview">
/// Overview and outcome of the narrative, each cut to 512 characters, with provenance; null until the narrative is
/// Completed.
/// </param>
/// <param name="SourceGlobalSequence">
/// <c>globalSequence</c> of the runtime event that ended the run and created this version of the record.
/// </param>
/// <param name="SourceRootSequence">Position of that event within the event sequence of its top-level run.</param>
/// <param name="SchemaVersion">Version of the record format, currently <c>1.0</c>.</param>
/// <param name="RecordUpdatedAtUtc">
/// When the record last changed: created, picked up by the worker, completed or failed.
/// </param>
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

/// <summary>
/// Identities of a durable run record. Except for the two run identifiers, the members are filled in when the facts
/// are assembled.
/// </summary>
/// <param name="RunId">Identifier of the process run the record describes.</param>
/// <param name="RootRunId">
/// Identifier of the top-level run of the run's subprocess tree; equals <c>runId</c> for a top-level run.
/// </param>
/// <param name="ParentRunId">
/// Identifier of the parent run of a child run; null for a top-level run and until the facts are assembled.
/// </param>
/// <param name="PlanId">
/// Identifier of the run's immutable launch plan (<c>launchPlanId</c> of the launch response); can be null until the
/// facts are assembled.
/// </param>
/// <param name="DefinitionId">
/// Identifier of the launched process definition; null until the facts are assembled or when the plan could not be
/// read.
/// </param>
/// <param name="DefinitionVersionId">
/// Identifier of the exact definition version that was launched; null in the same cases as <c>definitionId</c>.
/// </param>
/// <param name="ProjectId">
/// Identifier of the project the run was launched for; null for a run without a project, until the facts are
/// assembled, or when the stored value was invalid (warning <c>InvalidProjectId</c>).
/// </param>
internal sealed record ProcessRunRecordIdentityApiView(
    Guid RunId,
    Guid RootRunId,
    Guid? ParentRunId,
    Guid? PlanId,
    Guid? DefinitionId,
    Guid? DefinitionVersionId,
    Guid? ProjectId);

/// <summary>
/// Evidence sources behind the facts of a run record. Totals that depend on a missing source can be too low; for
/// example costs without <c>Pricing</c>.
/// </summary>
/// <param name="Available">
/// Sources that were complete, from: <c>RuntimeState</c>, <c>InstancePlan</c>, <c>StepAssignments</c>,
/// <c>ExecutionObservations</c>, <c>UsageTelemetry</c>, <c>Pricing</c>, <c>RuntimeEvents</c>,
/// <c>ArtifactLineage</c>, <c>Subprocesses</c>. Empty until the facts are assembled.
/// </param>
/// <param name="Missing">
/// Sources that were missing or incomplete, with the same names. Every source is listed here until the facts are
/// assembled.
/// </param>
internal sealed record ProcessRunRecordEvidenceApiView(
    IReadOnlyList<string> Available,
    IReadOnlyList<string> Missing);

/// <summary>
/// Timing, step, usage and cost totals of a run record. They cover the run and every child run it launched; until
/// the facts are assembled, all counts and costs are 0 and only <c>endedAtUtc</c> is known.
/// </summary>
/// <param name="StartedAtUtc">
/// Start of the run: its first runtime event, or else its first step activity; null until the facts are assembled
/// or when neither is known.
/// </param>
/// <param name="EndedAtUtc">When the run ended: the time of the event that created the record.</param>
/// <param name="DurationMilliseconds">
/// <c>endedAtUtc</c> minus <c>startedAtUtc</c> in whole milliseconds; null without a start, or when the end precedes
/// the start (warning <c>InvalidRunTiming</c>).
/// </param>
/// <param name="TotalStepCount">Number of steps of the run and its child runs, executable or not.</param>
/// <param name="ExecutableStepCount">Number of those steps that are executable.</param>
/// <param name="CompletedStepCount">Number of steps that were Completed when the facts were assembled.</param>
/// <param name="FailedStepCount">Number of steps that were Failed when the facts were assembled.</param>
/// <param name="CancelledStepCount">Number of steps that were Cancelled when the facts were assembled.</param>
/// <param name="RepetitionCount">Extra step attempts: the sum over all steps of attempts beyond the first.</param>
/// <param name="ExecutionCount">Number of distinct agent execution runs that executed steps.</param>
/// <param name="ReworkCount">Number of step rework requests (<c>StepReworkRequested</c> events).</param>
/// <param name="IncidentCount">Number of process manager incidents (<c>ManagerIncidentRaised</c> events).</param>
/// <param name="EscalationCount">
/// Number of loop budget escalations (<c>ManagerLoopBudgetEscalated</c> events).
/// </param>
/// <param name="InputTokenCount">Model input tokens reported by providers, summed over all usage observations.</param>
/// <param name="CachedInputTokenCount">Part of the input tokens that providers reported as served from cache.</param>
/// <param name="OutputTokenCount">Model output tokens reported by providers.</param>
/// <param name="ReasoningTokenCount">Reasoning tokens reported by providers.</param>
/// <param name="TotalTokenCount">
/// Total tokens as reported by the providers, not recalculated; whether it includes cached or reasoning tokens depends
/// on the provider.
/// </param>
/// <param name="EstimatedCost">
/// Cost in US dollars, rounded to 6 decimal places, of usage that was estimated (for example from metrics) rather
/// than observed. 0 can mean unpriced rather than free; check the <c>Pricing</c> evidence and the
/// <c>MissingPricing</c> warning.
/// </param>
/// <param name="ActualCost">
/// Cost in US dollars, rounded to 6 decimal places, of priced usage: the cost reported by the provider, calculated,
/// or taken from the price table. 0 can mean unpriced rather than free; check the <c>Pricing</c> evidence and the
/// <c>MissingPricing</c> warning.
/// </param>
/// <param name="ToolCallCount">Tool calls reported with the usage observations.</param>
/// <param name="ArtifactCount">Number of distinct artifacts produced or connected in the run.</param>
/// <param name="SubprocessCount">Number of child runs, at any depth, that were found for the run.</param>
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

/// <summary>
/// Hard facts of a run record: one page of steps, identifier lists, and runtime event statistics. They cover the run
/// and every child run it launched and are assembled from stored runtime data, not generated text.
/// </summary>
/// <param name="StepPage">Paging of <c>steps</c>.</param>
/// <param name="Steps">
/// The requested page of steps: the run's own steps first, then those of its child runs; within a run in plan order.
/// At most 2,048 steps are stored per record.
/// </param>
/// <param name="ParticipantIds">
/// Up to 200 participants: executor identifiers of step assignments and identifiers of agents that executed steps.
/// </param>
/// <param name="ParticipantCount">
/// Number of stored participants (at most 512; warning <c>ParticipantIdsTruncated</c> when more existed).
/// </param>
/// <param name="WorkflowIds">Up to 200 identifiers of workflows bound to steps.</param>
/// <param name="WorkflowCount">Number of stored workflow identifiers (at most 2,048).</param>
/// <param name="SubprocessRunIds">Up to 200 identifiers of child runs.</param>
/// <param name="SubprocessRunCount">Number of stored child run identifiers (at most 2,048).</param>
/// <param name="ExecutionRunIds">Up to 200 identifiers of agent execution runs that executed steps.</param>
/// <param name="ExecutionRunCount">Number of stored agent execution run identifiers (at most 4,096).</param>
/// <param name="ArtifactIds">Up to 200 identifiers of artifact instances produced or connected in the run.</param>
/// <param name="ArtifactCount">Number of stored artifact identifiers (at most 4,096).</param>
/// <param name="TotalRuntimeEventCount">Number of runtime events of the run and its child runs.</param>
/// <param name="ManagerRuntimeEventCount">
/// Number of those events that are process manager events (incidents, recovery and branch decisions, loop budget
/// escalations, subprocess messages).
/// </param>
/// <param name="RuntimeEventMinuteBucketPage">Paging of <c>runtimeEventMinuteBuckets</c>.</param>
/// <param name="RuntimeEventMinuteBuckets">
/// The requested page of per-minute event statistics, oldest minute first; only minutes with events are listed, and
/// at most the latest 10,080 minutes are stored.
/// </param>
/// <param name="RuntimeEventCategories">Event counts per category; only categories that occurred are listed.</param>
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

/// <summary>
/// Paging of the steps of a run record (<c>facts.stepPage</c>, <c>nodePage</c> of the graph).
/// </summary>
/// <param name="TotalCount">Number of stored steps across all pages.</param>
/// <param name="Offset">Number of steps skipped before this page (the applied <c>stepOffset</c>).</param>
/// <param name="Take">Requested page size (the applied <c>stepTake</c>).</param>
/// <param name="HasMore">
/// True when more steps follow this page; request the next page with <c>stepOffset</c> increased by <c>take</c>.
/// </param>
internal sealed record ProcessRunStepPageApiView(
    int TotalCount,
    int Offset,
    int Take,
    bool HasMore);

/// <summary>
/// Paging of the per-minute runtime event statistics of a run record.
/// </summary>
/// <param name="TotalCount">Number of stored per-minute buckets across all pages.</param>
/// <param name="Offset">
/// Number of buckets skipped before this page (the applied <c>runtimeEventMinuteOffset</c>).
/// </param>
/// <param name="Take">Requested page size (the applied <c>runtimeEventMinuteTake</c>).</param>
/// <param name="HasMore">
/// True when more buckets follow this page; request the next page with <c>runtimeEventMinuteOffset</c> increased by
/// <c>take</c>.
/// </param>
internal sealed record ProcessRunRuntimeEventMinuteBucketPageApiView(
    int TotalCount,
    int Offset,
    int Take,
    bool HasMore);

/// <summary>
/// Runtime event statistics of one UTC minute of a run.
/// </summary>
/// <param name="MinuteUtc">Start of the minute, in UTC.</param>
/// <param name="EventCount">Number of runtime events in that minute.</param>
/// <param name="ManagerEventCount">Number of those events that are process manager events.</param>
/// <param name="DurationMilliseconds">
/// Milliseconds between the first and the last event within that minute; 0 when the minute has one event.
/// </param>
internal sealed record ProcessRunRuntimeEventMinuteBucketApiView(
    DateTimeOffset MinuteUtc,
    int EventCount,
    int ManagerEventCount,
    long DurationMilliseconds);

/// <summary>
/// Runtime event count of one event category of a run.
/// </summary>
/// <param name="Category">
/// Event category, as one of the text tokens <c>RunLifecycle</c> (run created, activated, completed and similar),
/// <c>Step</c>, <c>Dispatch</c> (step claims and leases), <c>Manager</c> (process manager events) or <c>Other</c>.
/// </param>
/// <param name="EventCount">Number of events of this category.</param>
/// <param name="FirstOccurredAtUtc">When the first event of this category occurred.</param>
/// <param name="LastOccurredAtUtc">When the last event of this category occurred.</param>
internal sealed record ProcessRunRuntimeEventCategoryAggregateApiView(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ProcessRunRuntimeEventCategory>))]
    ProcessRunRuntimeEventCategory Category,
    int EventCount,
    DateTimeOffset FirstOccurredAtUtc,
    DateTimeOffset LastOccurredAtUtc);

/// <summary>
/// Hard facts of one step of a run record.
/// </summary>
/// <param name="OwningRunId">
/// Identifier of the run that owns the step: the record's run or one of its child runs.
/// </param>
/// <param name="StepInstanceId">Identifier of the step within its run.</param>
/// <param name="StepDefinitionId">Identifier of the step in the process definition.</param>
/// <param name="StepKey">Key of the step in the process definition; at most 256 characters.</param>
/// <param name="Outcome">
/// Status of the step when the facts were assembled, as text: <c>Pending</c> (planned, pending or ready),
/// <c>Running</c> (claimed or running), <c>Waiting</c>, <c>Blocked</c>, <c>Completed</c>, <c>Failed</c>,
/// <c>Cancelled</c> or <c>Skipped</c>; <c>Unknown</c> for an unrecognized status.
/// </param>
/// <param name="AttemptCount">Number of execution attempts of the step.</param>
/// <param name="ParticipantId">
/// Executor of the step assignment, or else the agent of the latest execution; null when neither is known.
/// </param>
/// <param name="WorkflowId">Identifier of the workflow bound to the step; null for steps without a workflow.</param>
/// <param name="DependencyStepIds">
/// Identifiers of the steps this step depends on (<c>stepInstanceId</c> values); at most 256.
/// </param>
/// <param name="ExecutionRunIds">Up to 64 identifiers of agent execution runs that executed the step.</param>
/// <param name="StartedAtUtc">
/// Earliest start of an execution or claim of the step; null when the step never started.
/// </param>
/// <param name="EndedAtUtc">Latest completion of an execution of the step; null when none completed.</param>
/// <param name="DurationMilliseconds">
/// <c>endedAtUtc</c> minus <c>startedAtUtc</c> in milliseconds; null when either is missing or when the end
/// precedes the start (warning <c>MissingStepTiming</c>).
/// </param>
/// <param name="InputTokenCount">Model input tokens attributed to the step.</param>
/// <param name="CachedInputTokenCount">Part of the input tokens served from cache.</param>
/// <param name="OutputTokenCount">Model output tokens attributed to the step.</param>
/// <param name="ReasoningTokenCount">Reasoning tokens attributed to the step.</param>
/// <param name="TotalTokenCount">Total tokens attributed to the step, as reported by the providers.</param>
/// <param name="EstimatedCost">
/// Estimated cost of the step in US dollars (see the record metrics); 0 can mean unpriced.
/// </param>
/// <param name="ActualCost">
/// Priced cost of the step in US dollars (see the record metrics); 0 can mean unpriced.
/// </param>
/// <param name="ToolCallCount">Tool calls attributed to the step.</param>
/// <param name="ArtifactCount">Number of artifacts attributed to the step.</param>
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

/// <summary>
/// Shortened narrative of a run record in its summary.
/// </summary>
/// <param name="Overview">Overview of the run, cut to 512 characters.</param>
/// <param name="Outcome">Outcome of the run, cut to 512 characters.</param>
/// <param name="Provenance">Which agent, execution and model produced the narrative.</param>
internal sealed record ProcessRunNarrativePreviewApiView(
    string Overview,
    string Outcome,
    ProcessRunNarrativeProvenanceApiView Provenance);

/// <summary>
/// Narrative of an ended run, generated by a process manager agent from the record's hard facts after they were
/// assembled. It is a model-written interpretation: the hard facts are the evidence. Text is trimmed, line breaks are
/// replaced by spaces, and long text is cut.
/// </summary>
/// <param name="Overview">Overview of the run; at most 2,048 characters.</param>
/// <param name="Outcome">Outcome of the run; at most 2,048 characters.</param>
/// <param name="WorkCompleted">Work that was done; at most 12 items of at most 512 characters.</param>
/// <param name="Problems">Problems that occurred; at most 12 items of at most 512 characters.</param>
/// <param name="Decisions">Decisions that were made; at most 12 items of at most 512 characters.</param>
/// <param name="FollowUps">Recommended follow-up work; at most 12 items of at most 512 characters.</param>
/// <param name="Provenance">Which agent, execution and model produced the narrative.</param>
internal sealed record ProcessRunNarrativeApiView(
    string Overview,
    string Outcome,
    IReadOnlyList<string> WorkCompleted,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> FollowUps,
    ProcessRunNarrativeProvenanceApiView Provenance);

/// <summary>
/// Origin of a run narrative.
/// </summary>
/// <param name="ManagerAgentId">
/// Identifier of the process manager agent that wrote the narrative, as a GUID string.
/// </param>
/// <param name="NarrativeExecutionRunId">
/// Identifier of the agent execution run that produced the narrative.
/// </param>
/// <param name="GenerationPolicyId">
/// Identifier of the generation policy, for example <c>process-run-narrative/v1</c>.
/// </param>
/// <param name="ModelId">Model that generated the narrative, or <c>unknown</c>.</param>
/// <param name="GeneratedAtUtc">When the narrative was generated.</param>
internal sealed record ProcessRunNarrativeProvenanceApiView(
    string ManagerAgentId,
    Guid NarrativeExecutionRunId,
    string GenerationPolicyId,
    string ModelId,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// One page of the step dependency graph of a run record, returned by
/// <c>GET /api/processes/runs/{runId}/graph</c>.
/// </summary>
/// <param name="Summary">Status, evidence and totals of the record, as in the summary operation.</param>
/// <param name="Nodes">The steps of this page; empty while the facts are not assembled.</param>
/// <param name="Edges">
/// Dependencies between steps of this page, ordered by target and then source step; at most 4,096.
/// </param>
/// <param name="SubprocessRunIds">Up to 200 identifiers of the run's child runs.</param>
/// <param name="NodePage">Paging of <c>nodes</c>.</param>
internal sealed record ProcessRunRecordGraphApiView(
    ProcessRunRecordSummaryApiView Summary,
    IReadOnlyList<ProcessRunRecordGraphNodeApiView> Nodes,
    IReadOnlyList<ProcessRunRecordGraphEdgeApiView> Edges,
    IReadOnlyList<Guid> SubprocessRunIds,
    ProcessRunStepPageApiView NodePage);

/// <summary>
/// One step of a run record's dependency graph, with its key facts.
/// </summary>
/// <param name="OwningRunId">
/// Identifier of the run that owns the step: the record's run or one of its child runs.
/// </param>
/// <param name="StepInstanceId">Identifier of the step within its run; edges refer to it.</param>
/// <param name="StepDefinitionId">Identifier of the step in the process definition.</param>
/// <param name="StepKey">Key of the step in the process definition; at most 256 characters.</param>
/// <param name="Outcome">
/// Status of the step when the facts were assembled, as text: <c>Pending</c>, <c>Running</c>, <c>Waiting</c>,
/// <c>Blocked</c>, <c>Completed</c>, <c>Failed</c>, <c>Cancelled</c>, <c>Skipped</c> or <c>Unknown</c>.
/// </param>
/// <param name="AttemptCount">Number of execution attempts of the step.</param>
/// <param name="ParticipantId">
/// Executor of the step assignment, or else the agent of the latest execution; null when neither is known.
/// </param>
/// <param name="WorkflowId">Identifier of the workflow bound to the step; null for steps without a workflow.</param>
/// <param name="StartedAtUtc">Earliest start of an execution or claim of the step; null when it never started.</param>
/// <param name="EndedAtUtc">Latest completion of an execution of the step; null when none completed.</param>
/// <param name="DurationMilliseconds">
/// <c>endedAtUtc</c> minus <c>startedAtUtc</c> in milliseconds; null when either is missing.
/// </param>
/// <param name="TotalTokenCount">Total tokens attributed to the step, as reported by the providers.</param>
/// <param name="EstimatedCost">Estimated cost of the step in US dollars; 0 can mean unpriced.</param>
/// <param name="ActualCost">Priced cost of the step in US dollars; 0 can mean unpriced.</param>
/// <param name="ToolCallCount">Tool calls attributed to the step.</param>
/// <param name="ArtifactCount">Number of artifacts attributed to the step.</param>
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

/// <summary>
/// A dependency between two steps of a run record's graph.
/// </summary>
/// <param name="SourceStepInstanceId">Identifier of the prerequisite step.</param>
/// <param name="TargetStepInstanceId">Identifier of the step that depends on the prerequisite step.</param>
/// <param name="Kind">Kind of the edge, as text; currently always <c>Dependency</c>.</param>
internal sealed record ProcessRunRecordGraphEdgeApiView(
    Guid SourceStepInstanceId,
    Guid TargetStepInstanceId,
    string Kind);

/// <summary>
/// Aggregates of the durable run records whose run ended within a window, returned by
/// <c>GET /api/processes/runs/analytics</c>. Usage, cost and activity sums cover only records with available facts,
/// and a parent record's totals already include its child runs, which are also counted as separate records.
/// </summary>
/// <param name="FromUtc">Effective inclusive start of the window.</param>
/// <param name="ToUtc">Effective exclusive end of the window.</param>
/// <param name="SchemaVersion">Version of the record format, currently <c>1.0</c>.</param>
/// <param name="MatchingRunCount">Number of records that match the window and filters.</param>
/// <param name="FactsAvailableRunCount">
/// Number of those records whose facts are assembled; the sums cover only these.
/// </param>
/// <param name="EvidenceCompleteRunCount">Number of records with facts whose completeness is <c>Complete</c>.</param>
/// <param name="EvidencePartialRunCount">Number of records with facts whose completeness is <c>Partial</c>.</param>
/// <param name="FactsUnavailableRunCount">
/// Number of matching records whose facts are not assembled yet or failed (<c>matchingRunCount</c> minus
/// <c>factsAvailableRunCount</c>).
/// </param>
/// <param name="DataThroughUtc">Latest end time among the matching records; null when nothing matched.</param>
/// <param name="SourceGlobalSequenceWatermark">
/// Highest <c>sourceGlobalSequence</c> among the matching records; null when nothing matched. Compare it between reads
/// to see whether new records arrived.
/// </param>
/// <param name="DurationMilliseconds">
/// Sum of run durations in milliseconds; a run without a duration counts as 0.
/// </param>
/// <param name="InputTokenCount">Sum of model input tokens.</param>
/// <param name="CachedInputTokenCount">Sum of cached input tokens.</param>
/// <param name="OutputTokenCount">Sum of model output tokens.</param>
/// <param name="ReasoningTokenCount">Sum of reasoning tokens.</param>
/// <param name="TotalTokenCount">Sum of provider-reported total tokens.</param>
/// <param name="EstimatedCost">
/// Sum of estimated costs in US dollars; 0 can mean unpriced (see the record metrics).
/// </param>
/// <param name="ActualCost">Sum of priced costs in US dollars; 0 can mean unpriced (see the record metrics).</param>
/// <param name="RepetitionCount">Sum of extra step attempts.</param>
/// <param name="ExecutionCount">Sum of agent execution runs.</param>
/// <param name="ReworkCount">Sum of step rework requests.</param>
/// <param name="IncidentCount">Sum of process manager incidents.</param>
/// <param name="EscalationCount">Sum of loop budget escalations.</param>
/// <param name="ToolCallCount">Sum of tool calls.</param>
/// <param name="ArtifactCount">Sum of artifacts.</param>
/// <param name="Dispositions">
/// Number of matching records per disposition; only dispositions that occur are listed. Covers every matching record,
/// with or without facts.
/// </param>
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

/// <summary>
/// Number of matching run records with one disposition.
/// </summary>
/// <param name="Disposition">
/// How the runs ended, as text: <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c> or <c>Blocked</c>.
/// </param>
/// <param name="MatchingRunCount">Number of matching records with this disposition.</param>
internal sealed record ProcessRunDispositionAnalyticsApiView(
    string Disposition,
    int MatchingRunCount);

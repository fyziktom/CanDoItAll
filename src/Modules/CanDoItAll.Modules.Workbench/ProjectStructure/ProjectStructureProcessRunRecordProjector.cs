using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessRunRecordProjector(
    IProcessRunRecordReader runRecordReader,
    ILogger<ProjectStructureProcessRunRecordProjector> logger)
{
    private const int MaximumProjectedRecordCount = 1000;

    public async Task<IReadOnlyDictionary<Guid, ProjectStructureProcessRunRecordProjection>> LoadAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var projections = new Dictionary<Guid, ProjectStructureProcessRunRecordProjection>();
        ProcessRunRecordCursor? cursor = null;
        while (projections.Count < MaximumProjectedRecordCount)
        {
            var remainingCapacity = MaximumProjectedRecordCount - projections.Count;
            var take = Math.Min(
                ProcessRunRecordPayloadLimits.MaximumPageSize,
                remainingCapacity);
            var page = await runRecordReader
                .ListAsync(
                    new ProcessRunRecordListQuery(take)
                    {
                        Payload = ProcessRunRecordListPayload.Full,
                        ProjectId = projectId,
                        Cursor = cursor,
                        RootRunsOnly = true
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var summary in page.Records.Take(remainingCapacity))
            {
                projections[summary.Identity.RunId.Value] = CreateProjection(summary);
            }

            if (page.NextCursor is null)
            {
                break;
            }

            if (page.NextCursor == cursor)
            {
                throw new InvalidOperationException(
                    $"Process run record pagination did not advance for project '{projectId:D}'.");
            }

            cursor = page.NextCursor;
        }

        if (projections.Count == MaximumProjectedRecordCount && cursor is not null)
        {
            logger.LogWarning(
                "Project structure process-run projection for project {ProjectId} reached the {RunRecordLimit} current-root record limit. Older runs remain available through the process run history API; narrow the project structure scope before increasing this limit.",
                projectId,
                MaximumProjectedRecordCount);
        }

        return projections;
    }

    private static ProjectStructureProcessRunRecordProjection CreateProjection(
        ProcessRunRecordSummary summary)
    {
        var metrics = summary.Metrics;
        return new ProjectStructureProcessRunRecordProjection(
            summary.Identity.RunId.Value,
            summary.Identity.RootRunId.Value,
            summary.Identity.PlanId?.Value,
            summary.Identity.DefinitionId?.Value,
            ResolveRuntimeStatus(summary.Disposition),
            summary.Disposition.ToString(),
            new ProjectStructureProcessRunProjectionStats(
                summary.Identity.RunId.Value,
                metrics.TotalStepCount,
                metrics.CompletedStepCount,
                BlockedStepCount: summary.Disposition == ProcessRunDisposition.Blocked ? 1 : 0,
                WaitingApprovalStepCount: 0,
                ActiveStepCount: 0),
            metrics.StartedAtUtc,
            metrics.EndedAtUtc,
            summary.UpdatedAtUtc,
            BuildSubtitle(summary),
            BuildNotes(summary),
            BuildMetadataJson(summary));
    }

    private static ProcessRuntimeStatus ResolveRuntimeStatus(ProcessRunDisposition disposition)
    {
        return disposition switch
        {
            ProcessRunDisposition.Succeeded => ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Failed => ProcessRuntimeStatus.Failed,
            ProcessRunDisposition.Cancelled => ProcessRuntimeStatus.Cancelled,
            ProcessRunDisposition.Blocked => ProcessRuntimeStatus.Blocked,
            ProcessRunDisposition.Escalated => ProcessRuntimeStatus.Escalated,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition,
                "Process run record disposition is not supported by the project structure projection.")
        };
    }

    private static string BuildSubtitle(ProcessRunRecordSummary summary)
    {
        if (summary.FactsStatus != ProcessRunFactsStatus.Completed)
        {
            return $"{summary.Disposition} · durable facts {summary.FactsStatus.ToString().ToLowerInvariant()}";
        }

        var metrics = summary.Metrics;
        var duration = metrics.DurationMilliseconds.HasValue
            ? TimeSpan.FromMilliseconds(metrics.DurationMilliseconds.Value).ToString(@"d\.hh\:mm\:ss")
            : "unknown duration";
        return FormattableString.Invariant(
            $"{summary.Disposition} · {metrics.CompletedStepCount}/{metrics.TotalStepCount} steps · {duration} · {metrics.TotalTokenCount:N0} tokens · {metrics.ActualCost:0.####} cost");
    }

    private static string BuildNotes(ProcessRunRecordSummary summary)
    {
        if (summary.FactsStatus != ProcessRunFactsStatus.Completed)
        {
            var retry = summary.FactsNextAttemptAtUtc is { } nextAttemptAtUtc
                ? $"Next retry: {nextAttemptAtUtc:u}"
                : summary.FactsStatus == ProcessRunFactsStatus.Failed
                    ? "No automatic retry remains."
                    : "Background assembly is pending.";
            var failure = string.IsNullOrWhiteSpace(summary.FactsLastErrorClass)
                ? "No facts-stage error is recorded."
                : $"Last error: {summary.FactsLastErrorClass}; diagnostic reference: {summary.FactsLastErrorDiagnosticReference ?? "unavailable"}.";
            return string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Durable process-run record ({summary.SchemaVersion}).",
                    $"Disposition: {summary.Disposition}.",
                    $"Facts status: {summary.FactsStatus}; attempts: {summary.FactsAttemptCount}.",
                    retry,
                    failure,
                    $"Run id: {summary.Identity.RunId.Value:D}",
                    $"Root run id: {summary.Identity.RootRunId.Value:D}",
                    $"Ended: {summary.Metrics.EndedAtUtc:u}"
                });
        }

        var metrics = summary.Metrics;
        var lines = new List<string>
        {
            $"Durable process-run record ({summary.SchemaVersion}).",
            $"Disposition: {summary.Disposition}.",
            $"Completeness: {summary.Completeness}; facts {summary.FactsStatus}; narrative {summary.NarrativeStatus}.",
            $"Run id: {summary.Identity.RunId.Value:D}",
            $"Root run id: {summary.Identity.RootRunId.Value:D}",
            $"Started: {metrics.StartedAtUtc?.ToString("u") ?? "unknown"}",
            $"Ended: {metrics.EndedAtUtc:u}",
            $"Steps: {metrics.CompletedStepCount}/{metrics.TotalStepCount} completed, {metrics.FailedStepCount} failed, {metrics.CancelledStepCount} cancelled, {metrics.RepetitionCount} repetitions.",
            FormattableString.Invariant(
                $"Usage: {metrics.TotalTokenCount:N0} tokens ({metrics.InputTokenCount:N0} input, {metrics.CachedInputTokenCount:N0} cached input, {metrics.OutputTokenCount:N0} output, {metrics.ReasoningTokenCount:N0} reasoning); estimated cost {metrics.EstimatedCost:0.####}; actual cost {metrics.ActualCost:0.####}."),
            $"Execution: {metrics.ExecutionCount} executions, {metrics.ToolCallCount} tool calls, {metrics.ArtifactCount} artifacts, {metrics.SubprocessCount} subprocesses.",
            $"Operational events: {metrics.ReworkCount} reworks, {metrics.IncidentCount} incidents, {metrics.EscalationCount} escalations.",
            $"Participants: {string.Join(", ", summary.ParticipantIds.Select(participantId => participantId.Value))}",
            $"Evidence available: {summary.AvailableEvidenceSources}.",
            $"Evidence missing: {summary.MissingEvidenceSources}."
        };

        if (summary.CompletenessWarnings.Count > 0)
        {
            lines.Add($"Completeness warnings: {string.Join("; ", summary.CompletenessWarnings)}");
        }

        if (summary.Narrative is { } narrative)
        {
            lines.Add(string.Empty);
            lines.Add("Manager summary");
            lines.Add(narrative.Overview);
            lines.Add($"Outcome: {narrative.Outcome}");
            AddNarrativeSection(lines, "Work completed", narrative.WorkCompleted);
            AddNarrativeSection(lines, "Problems", narrative.Problems);
            AddNarrativeSection(lines, "Decisions", narrative.Decisions);
            AddNarrativeSection(lines, "Follow-ups", narrative.FollowUps);
            lines.Add(
                $"Generated by manager {narrative.Provenance.ManagerAgentId.Value} using policy {narrative.Provenance.GenerationPolicyId} at {narrative.Provenance.GeneratedAtUtc:u}.");
        }
        else
        {
            lines.Add(
                summary.NarrativeStatus == ProcessRunNarrativeStatus.Failed
                    ? "Manager summary generation failed; the hard facts remain available."
                    : "Manager summary generation is pending.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildMetadataJson(ProcessRunRecordSummary summary)
    {
        return JsonSerializer.Serialize(new
        {
            processRunSummary = new
            {
                summary.Identity,
                Disposition = summary.Disposition.ToString(),
                LifecycleState = summary.LifecycleState.ToString(),
                Completeness = summary.Completeness.ToString(),
                AvailableEvidenceSources = summary.AvailableEvidenceSources.ToString(),
                MissingEvidenceSources = summary.MissingEvidenceSources.ToString(),
                summary.CompletenessWarnings,
                FactsStatus = summary.FactsStatus.ToString(),
                NarrativeStatus = summary.NarrativeStatus.ToString(),
                summary.Metrics,
                ParticipantIds = summary.ParticipantIds.Select(participantId => participantId.Value),
                summary.Narrative,
                summary.SourceGlobalSequence,
                summary.SourceRootSequence,
                summary.SchemaVersion,
                summary.UpdatedAtUtc
            }
        });
    }

    private static void AddNarrativeSection(
        ICollection<string> lines,
        string title,
        IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        lines.Add($"{title}:");
        foreach (var item in items)
        {
            lines.Add($"- {item}");
        }
    }
}

internal sealed record ProjectStructureProcessRunRecordProjection(
    Guid RunId,
    Guid RootRunId,
    Guid? PlanId,
    Guid? DefinitionId,
    ProcessRuntimeStatus RuntimeStatus,
    string Status,
    ProjectStructureProcessRunProjectionStats Stats,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Subtitle,
    string Notes,
    string MetadataJson);

internal sealed record ProjectStructureProcessRunProjectionStats(
    Guid RunId,
    int TotalStepCount,
    int CompletedStepCount,
    int BlockedStepCount,
    int WaitingApprovalStepCount,
    int ActiveStepCount)
{
    public static ProjectStructureProcessRunProjectionStats Empty(Guid runId)
    {
        return new ProjectStructureProcessRunProjectionStats(runId, 0, 0, 0, 0, 0);
    }
}

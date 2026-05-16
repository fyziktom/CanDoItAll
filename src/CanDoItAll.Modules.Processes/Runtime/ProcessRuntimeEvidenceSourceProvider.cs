using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeEvidenceSourceProvider(
    IDbContextFactory<AppDbContext> dbContextFactory) : IProcessRuntimeEvidenceSourceProvider
{
    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scopeId = request.ProcessRunId ?? Guid.Empty;
        var items = new List<MemorySourceItem>();

        items.AddRange((await FilterByRun(dbContext.Set<ProcessRun>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapRun));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessStepRun>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapStepRun));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessRunAssignment>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapAssignment));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessWorkBrief>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapWorkBrief));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessDecisionRecord>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapDecision));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessArtifactRecord>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapArtifact));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessJournalEntry>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapJournal));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessConformanceObservation>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapConformanceObservation));
        items.AddRange((await FilterByNullableProcessRunId(dbContext.Set<ProcessImprovementCandidate>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapImprovementCandidate));
        items.AddRange((await FilterByProcessRunId(dbContext.Set<ProcessWorkflowRunLink>().AsNoTracking(), request.ProcessRunId)
                .ToListAsync(cancellationToken))
            .Select(MapWorkflowRunLink));

        var allItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToList();
        var pageItems = MemorySourceSnapshotPage.Apply(
            allItems,
            request.Cursor,
            request.Take,
            out var nextCursor,
            out var hasMore);
        var snapshotHash = MemorySourceSnapshotHasher.Compute(allItems.Select(item => item.ContentHash).ToArray());

        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MemorySourceKind.ProcessRuntime, scopeId, snapshotHash),
                MemorySourceKind.ProcessRuntime,
                scopeId,
                DateTimeOffset.UtcNow,
                allItems.Count,
                nextCursor,
                hasMore),
            pageItems);
    }

    private static IQueryable<ProcessRun> FilterByRun(IQueryable<ProcessRun> query, Guid? processRunId)
        => processRunId.HasValue
            ? query.Where(item => item.Id == processRunId.Value)
            : query;

    private static IQueryable<T> FilterByProcessRunId<T>(IQueryable<T> query, Guid? processRunId)
        where T : class
        => processRunId.HasValue
            ? query.Where(item => EF.Property<Guid>(item, nameof(ProcessStepRun.ProcessRunId)) == processRunId.Value)
            : query;

    private static IQueryable<ProcessImprovementCandidate> FilterByNullableProcessRunId(
        IQueryable<ProcessImprovementCandidate> query,
        Guid? processRunId)
        => processRunId.HasValue
            ? query.Where(item => item.ProcessRunId == processRunId.Value)
            : query;

    private static MemorySourceItem MapRun(ProcessRun run)
    {
        var itemId = BuildItemId(run.Id, MemorySourceEntityKind.ProcessRun, run.Id);
        var content = BuildContent(
            ("Name", run.Name),
            ("Status", run.Status.ToString()),
            ("Operating mode", run.OperatingMode.ToString()),
            ("Trigger reason", run.TriggerReason),
            ("Manager", run.ManagerAgentName),
            ("Executor summary", run.ExecutorSnapshotSummary),
            ("Governance snapshot", RedactJson(run.GovernanceSnapshot)),
            ("Policy snapshot", RedactJson(run.PolicySnapshot)),
            ("Replay package key", run.ReplayPackageKey),
            ("First-time-right percent", run.FirstTimeRightPercent.ToString()),
            ("SLA attainment percent", run.SlaAttainmentPercent.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            run.Id.ToString("D"),
            run.ProcessDefinitionId.ToString("D"),
            run.ProcessDefinitionVersionId.ToString("D"),
            run.ParentRunId?.ToString("D"),
            run.ParentStepRunId?.ToString("D"),
            run.RootRunId?.ToString("D"),
            run.ProjectId?.ToString("D"),
            run.Name,
            run.Status.ToString(),
            run.OperatingMode.ToString(),
            run.TriggerReason,
            run.GovernanceSnapshot,
            run.PolicySnapshot,
            run.ExecutorSnapshotSummary,
            run.ManagerAgentId?.ToString("D"),
            run.ManagerAgentName,
            run.ReplayPackageKey,
            run.CreatedAtUtc.ToString("O"),
            run.UpdatedAtUtc.ToString("O"),
            run.StartedAtUtc?.ToString("O"),
            run.CompletedAtUtc?.ToString("O"),
            run.EstimatedCost.ToString(),
            run.ActualCost.ToString(),
            run.FirstTimeRightPercent.ToString(),
            run.SlaAttainmentPercent.ToString());

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessRun,
            run.Name,
            content,
            contentHash,
            run.CreatedAtUtc,
            run.UpdatedAtUtc,
            BuildProvenance(run.Id, MemorySourceEntityKind.ProcessRun, run.Id, $"/processes/runs/{run.Id:D}"),
            InternalRedactedPermission(
                HasPayload(run.GovernanceSnapshot) || HasPayload(run.PolicySnapshot),
                "Process run snapshots redact stored policy and governance payloads before exposure."),
            Layout: null,
            Links: BuildNullableLinks(run.Id, itemId, [
                run.ParentRunId is Guid parentRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessRun, parentRunId, "ParentRun")
                    : null,
                run.ParentStepRunId is Guid parentStepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, parentStepRunId, "ParentStepRun")
                    : null
            ]),
            References:
            [
                Reference("process-definition", run.ProcessDefinitionId, 0),
                Reference("process-definition-version", run.ProcessDefinitionVersionId, 1),
                Reference("project", run.ProjectId, 2),
                Reference("manager-agent", run.ManagerAgentId, 3)
            ],
            StorageReference: null,
            Metadata(
                ("status", run.Status.ToString()),
                ("operatingMode", run.OperatingMode.ToString()),
                ("definitionId", run.ProcessDefinitionId.ToString("D")),
                ("definitionVersionId", run.ProcessDefinitionVersionId.ToString("D")),
                ("projectId", run.ProjectId?.ToString("D") ?? string.Empty),
                ("hierarchyDepth", run.HierarchyDepth.ToString())));
    }

    private static MemorySourceItem MapStepRun(ProcessStepRun step)
    {
        var itemId = BuildItemId(step.ProcessRunId, MemorySourceEntityKind.ProcessStepRun, step.Id);
        var content = BuildContent(
            ("Title", step.Title),
            ("Status", step.Status.ToString()),
            ("Step kind", step.StepKind.ToString()),
            ("Role summary", step.RoleSnapshotSummary),
            ("Current executor", step.CurrentExecutorName),
            ("Decision summary", step.DecisionSummary),
            ("Blocked reason", step.BlockedReason),
            ("Refusal reason", step.RefusalReason),
            ("Exception summary", step.ExceptionSummary),
            ("Input quality", step.InputQualitySummary),
            ("Selected branch", step.SelectedBranchOutcomeTitle),
            ("Capability gap severity", step.CapabilityGapSeverity.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            step.Id.ToString("D"),
            step.ProcessRunId.ToString("D"),
            step.StepDefinitionId.ToString("D"),
            step.Sequence.ToString(),
            step.Title,
            step.StepKind.ToString(),
            step.Status.ToString(),
            step.RoleSnapshotSummary,
            step.CurrentExecutorName,
            step.CurrentExecutorPartyId?.ToString("D"),
            step.DecisionSummary,
            step.BlockedReason,
            step.RefusalReason,
            step.ExceptionSummary,
            step.InputQualitySummary,
            step.SelectedBranchOutcomeId?.ToString("D"),
            step.SelectedBranchOutcomeTitle,
            step.ReadyAtUtc?.ToString("O"),
            step.StartedAtUtc?.ToString("O"),
            step.CompletedAtUtc?.ToString("O"),
            step.WaitMinutes.ToString(),
            step.TouchMinutes.ToString(),
            step.BlockedMinutes.ToString(),
            step.ReworkCount.ToString(),
            step.CapabilityGapSeverity.ToString());

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessStepRun,
            step.Title,
            content,
            contentHash,
            step.ReadyAtUtc,
            step.CompletedAtUtc ?? step.StartedAtUtc ?? step.ReadyAtUtc,
            BuildProvenance(step.ProcessRunId, MemorySourceEntityKind.ProcessStepRun, step.Id, $"/processes/runs/{step.ProcessRunId:D}/steps/{step.Id:D}"),
            InternalReadOnlyPermission("Process step snapshots expose runtime summaries and status transitions."),
            new MemorySourceLayoutMetadata(
                X: null,
                Y: null,
                ZIndex: null,
                step.ReadyAtUtc,
                step.CompletedAtUtc,
                step.TouchMinutes > 0 ? step.TouchMinutes * 60 : null,
                "process-runtime",
                "{}"),
            Links: BuildLinks(step.ProcessRunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, step.ProcessRunId, "BelongsToRun")]),
            References:
            [
                Reference("step-definition", step.StepDefinitionId, 0),
                Reference("current-executor-party", step.CurrentExecutorPartyId, 1),
                Reference("selected-branch-outcome", step.SelectedBranchOutcomeId, 2)
            ],
            StorageReference: null,
            Metadata(
                ("sequence", step.Sequence.ToString()),
                ("status", step.Status.ToString()),
                ("stepKind", step.StepKind.ToString()),
                ("capabilityGapSeverity", step.CapabilityGapSeverity.ToString())));
    }

    private static MemorySourceItem MapAssignment(ProcessRunAssignment assignment)
    {
        var itemId = BuildItemId(assignment.ProcessRunId, MemorySourceEntityKind.ProcessRunAssignment, assignment.Id);
        var content = BuildContent(
            ("Display name", assignment.DisplayName),
            ("Executor kind", assignment.ExecutorKind),
            ("Binding reason", assignment.BindingReason),
            ("Source registry key", assignment.SourceRegistryKey),
            ("Snapshot summary", assignment.SnapshotSummary),
            ("Fallback", assignment.IsFallback.ToString()),
            ("Capability gap", assignment.IsCapabilityGap.ToString()),
            ("Direct messaging", assignment.AllowsDirectMessaging.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            assignment.Id.ToString("D"),
            assignment.ProcessRunId.ToString("D"),
            assignment.RoleRequirementId.ToString("D"),
            assignment.StepDefinitionId?.ToString("D"),
            assignment.PartyId?.ToString("D"),
            assignment.DisplayName,
            assignment.ExecutorKind,
            assignment.WorkflowDefinitionId?.ToString("D"),
            assignment.WorkflowVersionId?.ToString("D"),
            assignment.BindingReason,
            assignment.SourceRegistryKey,
            assignment.SnapshotSummary,
            assignment.IsFallback.ToString(),
            assignment.IsCapabilityGap.ToString(),
            assignment.AllowsDirectMessaging.ToString());

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessRunAssignment,
            assignment.DisplayName,
            content,
            contentHash,
            CreatedAtUtc: null,
            UpdatedAtUtc: null,
            BuildProvenance(assignment.ProcessRunId, MemorySourceEntityKind.ProcessRunAssignment, assignment.Id, $"/processes/runs/{assignment.ProcessRunId:D}/assignments/{assignment.Id:D}"),
            InternalReadOnlyPermission("Process assignment snapshots expose binding metadata and executor summaries."),
            Layout: null,
            Links: BuildLinks(assignment.ProcessRunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, assignment.ProcessRunId, "BelongsToRun")]),
            References:
            [
                Reference("role-requirement", assignment.RoleRequirementId, 0),
                Reference("step-definition", assignment.StepDefinitionId, 1),
                Reference("party", assignment.PartyId, 2),
                Reference("workflow-definition", assignment.WorkflowDefinitionId, 3),
                Reference("workflow-version", assignment.WorkflowVersionId, 4)
            ],
            StorageReference: null,
            Metadata(
                ("executorKind", assignment.ExecutorKind),
                ("sourceRegistryKey", assignment.SourceRegistryKey),
                ("isFallback", assignment.IsFallback.ToString()),
                ("isCapabilityGap", assignment.IsCapabilityGap.ToString()),
                ("allowsDirectMessaging", assignment.AllowsDirectMessaging.ToString())));
    }

    private static MemorySourceItem MapWorkBrief(ProcessWorkBrief brief)
    {
        var itemId = BuildItemId(brief.ProcessRunId, MemorySourceEntityKind.ProcessWorkBrief, brief.Id);
        var content = BuildContent(
            ("Title", brief.Title),
            ("Work brief", brief.WorkBriefText),
            ("Handoff summary", brief.HandoffSummary),
            ("Assignment reason", brief.AssignmentReason),
            ("Expected outcome", brief.ExpectedOutcome),
            ("Evidence expectation", brief.EvidenceExpectationSummary));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            brief.Id.ToString("D"),
            brief.ProcessRunId.ToString("D"),
            brief.StepRunId?.ToString("D"),
            brief.Title,
            brief.WorkBriefText,
            brief.HandoffSummary,
            brief.AssignmentReason,
            brief.ExpectedOutcome,
            brief.EvidenceExpectationSummary,
            brief.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessWorkBrief,
            brief.Title,
            content,
            contentHash,
            brief.CreatedAtUtc,
            brief.CreatedAtUtc,
            BuildProvenance(brief.ProcessRunId, MemorySourceEntityKind.ProcessWorkBrief, brief.Id, $"/processes/runs/{brief.ProcessRunId:D}/work-briefs/{brief.Id:D}"),
            InternalReadOnlyPermission("Process work brief snapshots expose task instructions and evidence expectations."),
            Layout: null,
            Links: BuildNullableLinks(brief.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, brief.ProcessRunId, "BelongsToRun"),
                brief.StepRunId is Guid stepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, stepRunId, "DescribesStep")
                    : null
            ]),
            References: [Reference("step-run", brief.StepRunId, 0)],
            StorageReference: null,
            Metadata(("stepRunId", brief.StepRunId?.ToString("D") ?? string.Empty)));
    }

    private static MemorySourceItem MapDecision(ProcessDecisionRecord decision)
    {
        var itemId = BuildItemId(decision.ProcessRunId, MemorySourceEntityKind.ProcessDecision, decision.Id);
        var content = BuildContent(
            ("Title", decision.Title),
            ("Kind", decision.DecisionKind.ToString()),
            ("Outcome", decision.Outcome.ToString()),
            ("Reason", decision.Reason),
            ("Policy evaluation", decision.PolicyEvaluation),
            ("Branch outcome", decision.BranchOutcomeTitle),
            ("Decided by", decision.DecidedBy),
            ("Operating mode", decision.OperatingMode.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            decision.Id.ToString("D"),
            decision.ProcessRunId.ToString("D"),
            decision.StepRunId?.ToString("D"),
            decision.DecisionKind.ToString(),
            decision.Outcome.ToString(),
            decision.Title,
            decision.Reason,
            decision.PolicyEvaluation,
            decision.BranchOutcomeId?.ToString("D"),
            decision.BranchOutcomeTitle,
            decision.DecidedBy,
            decision.OperatingMode.ToString(),
            decision.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessDecision,
            decision.Title,
            content,
            contentHash,
            decision.CreatedAtUtc,
            decision.CreatedAtUtc,
            BuildProvenance(decision.ProcessRunId, MemorySourceEntityKind.ProcessDecision, decision.Id, $"/processes/runs/{decision.ProcessRunId:D}/decisions/{decision.Id:D}"),
            InternalReadOnlyPermission("Process decision snapshots expose governance decisions and policy evaluation summaries."),
            Layout: null,
            Links: BuildNullableLinks(decision.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, decision.ProcessRunId, "BelongsToRun"),
                decision.StepRunId is Guid stepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, stepRunId, "DecidesStep")
                    : null
            ]),
            References:
            [
                Reference("step-run", decision.StepRunId, 0),
                Reference("branch-outcome", decision.BranchOutcomeId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("decisionKind", decision.DecisionKind.ToString()),
                ("outcome", decision.Outcome.ToString()),
                ("operatingMode", decision.OperatingMode.ToString())));
    }

    private static MemorySourceItem MapArtifact(ProcessArtifactRecord artifact)
    {
        var itemId = BuildItemId(artifact.ProcessRunId, MemorySourceEntityKind.ProcessArtifact, artifact.Id);
        var sensitivity = MapSensitivity(artifact.SensitivityLevel);
        var containsSensitivePayload = artifact.SensitivityLevel is ProcessSensitivityLevel.Confidential or ProcessSensitivityLevel.Restricted;
        var content = BuildContent(
            ("Title", artifact.Title),
            ("Kind", artifact.ArtifactKind.ToString()),
            ("Trust status", artifact.TrustStatus.ToString()),
            ("Sensitivity", artifact.SensitivityLevel.ToString()),
            ("Provenance", artifact.ProvenanceSummary),
            ("Allowed future usage", artifact.AllowedFutureUsageSummary),
            ("Review summary", artifact.ReviewSummary),
            ("External reference", artifact.ExternalReferenceKey));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            artifact.Id.ToString("D"),
            artifact.ProcessRunId.ToString("D"),
            artifact.StepRunId?.ToString("D"),
            artifact.ArtifactExpectationId?.ToString("D"),
            artifact.ArtifactKind.ToString(),
            artifact.Title,
            artifact.TrustStatus.ToString(),
            artifact.SensitivityLevel.ToString(),
            artifact.ProvenanceSummary,
            artifact.AllowedFutureUsageSummary,
            artifact.ReviewSummary,
            artifact.ManagedStoragePath,
            artifact.ExternalReferenceKey,
            artifact.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessArtifact,
            artifact.Title,
            content,
            contentHash,
            artifact.CreatedAtUtc,
            artifact.CreatedAtUtc,
            BuildProvenance(artifact.ProcessRunId, MemorySourceEntityKind.ProcessArtifact, artifact.Id, $"/processes/runs/{artifact.ProcessRunId:D}/artifacts/{artifact.Id:D}"),
            new MemorySourcePermissionContext(
                containsSensitivePayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
                sensitivity,
                containsSensitivePayload,
                "Process artifact snapshots expose summaries and storage locators, not artifact payload bytes.",
                artifact.AllowedFutureUsageSummary),
            Layout: null,
            Links: BuildNullableLinks(artifact.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, artifact.ProcessRunId, "BelongsToRun"),
                artifact.StepRunId is Guid stepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, stepRunId, "EvidenceForStep")
                    : null
            ]),
            References:
            [
                Reference("step-run", artifact.StepRunId, 0),
                Reference("artifact-expectation", artifact.ArtifactExpectationId, 1)
            ],
            StorageReference: ResolveStorageReference(artifact),
            Metadata: Metadata(
                ("artifactKind", artifact.ArtifactKind.ToString()),
                ("trustStatus", artifact.TrustStatus.ToString()),
                ("sensitivityLevel", artifact.SensitivityLevel.ToString()),
                ("externalReferenceKey", artifact.ExternalReferenceKey)));
    }

    private static MemorySourceItem MapJournal(ProcessJournalEntry journal)
    {
        var itemId = BuildItemId(journal.ProcessRunId, MemorySourceEntityKind.ProcessJournal, journal.Id);
        var hasReplayPayload = HasPayload(journal.ReplayContextJson);
        var content = BuildContent(
            ("Title", journal.Title),
            ("Event type", journal.EventType),
            ("Description", journal.Description),
            ("Correlation id", journal.CorrelationId),
            ("Operating mode", journal.OperatingMode.ToString()),
            ("Policy version", journal.PolicyVersion),
            ("Environment mode", journal.EnvironmentMode),
            ("Replay context", RedactJson(journal.ReplayContextJson)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            journal.Id.ToString("D"),
            journal.ProcessRunId.ToString("D"),
            journal.StepRunId?.ToString("D"),
            journal.EventType,
            journal.Title,
            journal.Description,
            journal.CorrelationId,
            journal.OperatingMode.ToString(),
            journal.PolicyVersion,
            journal.EnvironmentMode,
            journal.ReplayContextJson,
            journal.OccurredAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessJournal,
            journal.Title,
            content,
            contentHash,
            journal.OccurredAtUtc,
            journal.OccurredAtUtc,
            BuildProvenance(journal.ProcessRunId, MemorySourceEntityKind.ProcessJournal, journal.Id, $"/processes/runs/{journal.ProcessRunId:D}/journal/{journal.Id:D}"),
            InternalRedactedPermission(
                hasReplayPayload,
                "Process journal snapshots redact replay context payloads before exposure."),
            Layout: null,
            Links: BuildNullableLinks(journal.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, journal.ProcessRunId, "BelongsToRun"),
                journal.StepRunId is Guid stepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, stepRunId, "ReportsStep")
                    : null
            ]),
            References: [Reference("step-run", journal.StepRunId, 0)],
            StorageReference: null,
            Metadata(
                ("eventType", journal.EventType),
                ("correlationId", journal.CorrelationId),
                ("policyVersion", journal.PolicyVersion),
                ("environmentMode", journal.EnvironmentMode)));
    }

    private static MemorySourceItem MapConformanceObservation(ProcessConformanceObservation observation)
    {
        var itemId = BuildItemId(observation.ProcessRunId, MemorySourceEntityKind.ProcessConformanceObservation, observation.Id);
        var content = BuildContent(
            ("Category", observation.Category),
            ("Severity", observation.Severity.ToString()),
            ("Observation", observation.Observation),
            ("Deviation reason", observation.DeviationReason),
            ("Safe non-action", observation.IsSafeNonAction.ToString()),
            ("Contains sensitive assessment", observation.ContainsSensitiveAssessment.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            observation.Id.ToString("D"),
            observation.ProcessRunId.ToString("D"),
            observation.StepRunId?.ToString("D"),
            observation.Severity.ToString(),
            observation.Category,
            observation.Observation,
            observation.DeviationReason,
            observation.IsSafeNonAction.ToString(),
            observation.ContainsSensitiveAssessment.ToString(),
            observation.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessConformanceObservation,
            observation.Category,
            content,
            contentHash,
            observation.CreatedAtUtc,
            observation.CreatedAtUtc,
            BuildProvenance(observation.ProcessRunId, MemorySourceEntityKind.ProcessConformanceObservation, observation.Id, $"/processes/runs/{observation.ProcessRunId:D}/conformance/{observation.Id:D}"),
            new MemorySourcePermissionContext(
                observation.ContainsSensitiveAssessment ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
                observation.ContainsSensitiveAssessment ? MemorySourceSensitivity.Sensitive : MemorySourceSensitivity.Internal,
                observation.ContainsSensitiveAssessment,
                "Process conformance snapshots mark sensitive assessments for downstream redaction-aware use.",
                "Governance review and process quality improvement evidence."),
            Layout: null,
            Links: BuildNullableLinks(observation.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, observation.ProcessRunId, "BelongsToRun"),
                observation.StepRunId is Guid stepRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessStepRun, stepRunId, "ObservesStep")
                    : null
            ]),
            References: [Reference("step-run", observation.StepRunId, 0)],
            StorageReference: null,
            Metadata(
                ("severity", observation.Severity.ToString()),
                ("category", observation.Category),
                ("isSafeNonAction", observation.IsSafeNonAction.ToString()),
                ("containsSensitiveAssessment", observation.ContainsSensitiveAssessment.ToString())));
    }

    private static MemorySourceItem MapImprovementCandidate(ProcessImprovementCandidate candidate)
    {
        var scopeId = candidate.ProcessRunId ?? Guid.Empty;
        var itemId = BuildItemId(scopeId, MemorySourceEntityKind.ProcessImprovementCandidate, candidate.Id);
        var content = BuildContent(
            ("Title", candidate.Title),
            ("Category", candidate.Category),
            ("Problem summary", candidate.ProblemSummary),
            ("Evidence summary", candidate.EvidenceSummary),
            ("Status", candidate.Status.ToString()),
            ("Training opportunity", candidate.IsTrainingOpportunity.ToString()),
            ("Requires governance review", candidate.RequiresGovernanceReview.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            candidate.Id.ToString("D"),
            candidate.ProcessDefinitionId.ToString("D"),
            candidate.ProcessRunId?.ToString("D"),
            candidate.Title,
            candidate.Category,
            candidate.ProblemSummary,
            candidate.EvidenceSummary,
            candidate.Status.ToString(),
            candidate.IsTrainingOpportunity.ToString(),
            candidate.RequiresGovernanceReview.ToString(),
            candidate.CreatedAtUtc.ToString("O"),
            candidate.ClosedAtUtc?.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessImprovementCandidate,
            candidate.Title,
            content,
            contentHash,
            candidate.CreatedAtUtc,
            candidate.ClosedAtUtc ?? candidate.CreatedAtUtc,
            BuildProvenance(scopeId, MemorySourceEntityKind.ProcessImprovementCandidate, candidate.Id, $"/processes/improvements/{candidate.Id:D}"),
            InternalReadOnlyPermission("Process improvement snapshots expose problem and evidence summaries."),
            Layout: null,
            Links: BuildNullableLinks(scopeId, itemId, [
                candidate.ProcessRunId is Guid processRunId
                    ? new LinkTarget(MemorySourceEntityKind.ProcessRun, processRunId, "ImprovesRun")
                    : null
            ]),
            References:
            [
                Reference("process-definition", candidate.ProcessDefinitionId, 0),
                Reference("process-run", candidate.ProcessRunId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("status", candidate.Status.ToString()),
                ("category", candidate.Category),
                ("isTrainingOpportunity", candidate.IsTrainingOpportunity.ToString()),
                ("requiresGovernanceReview", candidate.RequiresGovernanceReview.ToString())));
    }

    private static MemorySourceItem MapWorkflowRunLink(ProcessWorkflowRunLink link)
    {
        var itemId = BuildItemId(link.ProcessRunId, MemorySourceEntityKind.ProcessWorkflowRunLink, link.Id);
        var content = BuildContent(
            ("Workflow run", link.WorkflowRunId.ToString("D")),
            ("Workflow backend", link.WorkflowBackend.ToString()),
            ("Backend run id", link.WorkflowBackendRunId),
            ("State", link.State.ToString()),
            ("Summary", link.Summary));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            link.Id.ToString("D"),
            link.ProcessRunId.ToString("D"),
            link.StepRunId.ToString("D"),
            link.AssignmentId.ToString("D"),
            link.WorkflowDefinitionId.ToString("D"),
            link.WorkflowVersionId.ToString("D"),
            link.WorkflowRunId.ToString("D"),
            link.WorkflowBackend.ToString(),
            link.WorkflowBackendRunId,
            link.State.ToString(),
            link.Summary,
            link.CreatedAtUtc.ToString("O"),
            link.UpdatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessWorkflowRunLink,
            $"Workflow run {link.WorkflowRunId:D}",
            content,
            contentHash,
            link.CreatedAtUtc,
            link.UpdatedAtUtc,
            BuildProvenance(link.ProcessRunId, MemorySourceEntityKind.ProcessWorkflowRunLink, link.Id, $"/processes/runs/{link.ProcessRunId:D}/workflow-runs/{link.WorkflowRunId:D}"),
            InternalReadOnlyPermission("Process workflow links expose runtime bridge metadata between process assignments and workflow runs."),
            Layout: null,
            Links: BuildLinks(link.ProcessRunId, itemId, [
                new LinkTarget(MemorySourceEntityKind.ProcessRun, link.ProcessRunId, "BelongsToRun"),
                new LinkTarget(MemorySourceEntityKind.ProcessStepRun, link.StepRunId, "ExecutesStep"),
                new LinkTarget(MemorySourceEntityKind.ProcessRunAssignment, link.AssignmentId, "UsesAssignment")
            ]),
            References:
            [
                Reference("workflow-definition", link.WorkflowDefinitionId, 0),
                Reference("workflow-version", link.WorkflowVersionId, 1),
                Reference("workflow-run", link.WorkflowRunId, 2)
            ],
            StorageReference: null,
            Metadata(
                ("workflowBackend", link.WorkflowBackend.ToString()),
                ("workflowBackendRunId", link.WorkflowBackendRunId),
                ("state", link.State.ToString())));
    }

    private static MemorySourceItemId BuildItemId(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        Guid sourceEntityId)
        => MemorySourceItemId.Create(
            MemorySourceKind.ProcessRuntime,
            scopeId,
            entityKind,
            sourceEntityId.ToString("D"));

    private static MemorySourceProvenance BuildProvenance(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        Guid sourceEntityId,
        string sourceRoute)
        => new(
            MemorySourceKind.ProcessRuntime,
            scopeId,
            entityKind,
            sourceEntityId.ToString("D"),
            sourceRoute);

    private static MemorySourcePermissionContext InternalReadOnlyPermission(string redactionPolicy)
        => new(
            MemorySourceAccessMode.ReadOnly,
            MemorySourceSensitivity.Internal,
            ContainsSensitivePayload: false,
            redactionPolicy,
            "Source-grounded process runtime evidence.");

    private static MemorySourcePermissionContext InternalRedactedPermission(
        bool containsSensitivePayload,
        string redactionPolicy)
        => new(
            containsSensitivePayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
            MemorySourceSensitivity.Internal,
            containsSensitivePayload,
            redactionPolicy,
            "Source-grounded process runtime evidence.");

    private static MemorySourceSensitivity MapSensitivity(ProcessSensitivityLevel sensitivityLevel)
        => sensitivityLevel switch
        {
            ProcessSensitivityLevel.Public => MemorySourceSensitivity.Public,
            ProcessSensitivityLevel.Internal => MemorySourceSensitivity.Internal,
            ProcessSensitivityLevel.Confidential => MemorySourceSensitivity.Confidential,
            ProcessSensitivityLevel.Restricted => MemorySourceSensitivity.Sensitive,
            _ => MemorySourceSensitivity.Internal
        };

    private static string BuildContent(params (string Label, string? Value)[] fields)
        => string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"{field.Label}: {WorkflowExecutorRedaction.RedactText(field.Value)}"));

    private static string RedactJson(string? json)
        => HasPayload(json) ? WorkflowExecutorRedaction.RedactSettingsJson(json) : string.Empty;

    private static bool HasPayload(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value.Trim(), "{}", StringComparison.Ordinal);

    private static IReadOnlyList<MemorySourceLink> BuildLinks(
        Guid scopeId,
        MemorySourceItemId sourceId,
        IReadOnlyList<LinkTarget> targets)
        => targets
            .Select(target => new MemorySourceLink(
                sourceId,
                BuildItemId(scopeId, target.EntityKind, target.EntityId),
                target.Kind,
                IsUserAuthored: false))
            .OrderBy(link => link.TargetId.Value, StringComparer.Ordinal)
            .ThenBy(link => link.Kind, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<MemorySourceLink> BuildNullableLinks(
        Guid scopeId,
        MemorySourceItemId sourceId,
        IReadOnlyList<LinkTarget?> targets)
        => BuildLinks(scopeId, sourceId, targets.OfType<LinkTarget>().ToList());

    private static MemorySourceReference Reference(string referenceKind, Guid referenceId, int orderIndex)
        => new(referenceKind, referenceId.ToString("D"), orderIndex);

    private static MemorySourceReference Reference(string referenceKind, Guid? referenceId, int orderIndex)
        => new(referenceKind, referenceId?.ToString("D") ?? string.Empty, orderIndex);

    private static MemorySourceStorageReference? ResolveStorageReference(ProcessArtifactRecord artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
        {
            return new MemorySourceStorageReference(
                "processes",
                "managed-storage-path",
                artifact.ManagedStoragePath.Trim(),
                string.Empty,
                artifact.Title);
        }

        if (string.IsNullOrWhiteSpace(artifact.ExternalReferenceKey))
        {
            return null;
        }

        return new MemorySourceStorageReference(
            "processes",
            "external-reference-key",
            artifact.ExternalReferenceKey.Trim(),
            string.Empty,
            artifact.Title);
    }

    private static IReadOnlyDictionary<string, string> Metadata(params (string Key, string Value)[] values)
        => values.ToDictionary(
            value => value.Key,
            value => value.Value,
            StringComparer.Ordinal);

    private sealed record LinkTarget(
        MemorySourceEntityKind EntityKind,
        Guid EntityId,
        string Kind);
}

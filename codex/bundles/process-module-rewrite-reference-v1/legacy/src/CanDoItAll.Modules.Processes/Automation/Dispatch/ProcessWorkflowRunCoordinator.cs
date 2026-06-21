using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessWorkflowRunCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkflowCatalogService workflowCatalogService,
    IWorkflowProcessExecutorBridge workflowProcessExecutorBridge,
    IWorkflowRunStore workflowRunStore,
    IClock clock,
    ILogger<ProcessWorkflowRunCoordinator> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProcessWorkflowExecutionOutcome> TryRunOrObserveAsync(
        Guid processRunId,
        Guid stepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var context = await LoadWorkflowDispatchContextAsync(dbContext, processRunId, stepRunId, cancellationToken);
        if (context is null)
        {
            return ProcessWorkflowExecutionOutcome.NotHandled;
        }

        if (!TryResolveWorkflowReference(context.Assignment, context.Role, out var workflowId, out var workflowVersionId, out var missingReferenceReason))
        {
            return ProcessWorkflowExecutionOutcome.CreateHandled(
                ProcessStepRunStatus.Failed,
                missingReferenceReason,
                null);
        }

        var existingLink = await dbContext.Set<ProcessWorkflowRunLink>()
            .SingleOrDefaultAsync(
                item => item.StepRunId == stepRunId && item.AssignmentId == context.Assignment.Id,
                cancellationToken);
        if (existingLink is not null)
        {
            var observedRun = await workflowRunStore.GetRunAsync(new WorkflowRunId(existingLink.WorkflowRunId), cancellationToken);
            if (observedRun is null)
            {
                return ProcessWorkflowExecutionOutcome.CreateHandled(
                    ProcessStepRunStatus.Failed,
                    $"Workflow run '{existingLink.WorkflowRunId:D}' is linked to the process step but was not found in the workflow run store.",
                    existingLink);
            }

            UpdateLink(existingLink, observedRun, clock.GetUtcNow());
            await ProjectWorkflowArtifactsAsync(dbContext, context, existingLink, observedRun, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ProcessWorkflowExecutionOutcome.CreateHandled(
                MapWorkflowStateToStepStatus(observedRun.State),
                BuildWorkflowOutcomeReason(observedRun),
                existingLink);
        }

        var workflowDetail = await workflowCatalogService.GetDefinitionAsync(workflowId, workflowVersionId, cancellationToken);
        if (workflowDetail is null)
        {
            return ProcessWorkflowExecutionOutcome.CreateHandled(
                ProcessStepRunStatus.Failed,
                $"Workflow definition '{workflowId}' version '{workflowVersionId}' was not found.",
                null);
        }

        if (!workflowDetail.Validation.Succeeded)
        {
            var validationSummary = string.Join(
                " | ",
                workflowDetail.Validation.Issues
                    .Select(issue => $"{issue.Code}: {issue.Message}")
                    .Take(5));
            return ProcessWorkflowExecutionOutcome.CreateHandled(
                ProcessStepRunStatus.Failed,
                $"Workflow definition '{workflowDetail.Definition.Name}' is not valid for process execution. {validationSummary}",
                null);
        }

        var inputJson = BuildWorkflowInputJson(context, trigger);
        var run = await workflowProcessExecutorBridge.StartForProcessAssignmentAsync(
            workflowDetail.Definition,
            new WorkflowRunStartRequest(
                workflowDetail.Definition.Id,
                workflowDetail.Definition.VersionId,
                inputJson,
                RequestedBackend: null,
                SourceProcessRunId: context.Run.Id,
                SourceProcessAssignmentId: context.Assignment.Id),
            cancellationToken);
        var now = clock.GetUtcNow();
        var link = new ProcessWorkflowRunLink
        {
            ProcessRunId = context.Run.Id,
            StepRunId = context.StepRun.Id,
            AssignmentId = context.Assignment.Id,
            WorkflowDefinitionId = run.WorkflowId.Value,
            WorkflowVersionId = run.VersionId.Value,
            WorkflowRunId = run.RunId.Value,
            WorkflowBackend = run.Backend,
            WorkflowBackendRunId = run.BackendRunId,
            State = run.State,
            Summary = run.Summary,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await dbContext.Set<ProcessWorkflowRunLink>().AddAsync(link, cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildWorkflowJournalEntry(
                context.Run,
                context.StepRun.Id,
                ProcessRuntimeEventTypes.WorkflowRunStarted,
                "Started workflow run",
                $"Started workflow '{workflowDetail.Definition.Name}' ({run.RunId}) for assignment '{context.Assignment.DisplayName}'.",
                run.RunId.Value.ToString("N"),
                inputJson,
                now),
            cancellationToken);
        await ProjectWorkflowArtifactsAsync(dbContext, context, link, run, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Started workflow run {WorkflowRunId} for process run {ProcessRunId}, step {StepRunId}, assignment {AssignmentId}.",
            run.RunId.Value,
            context.Run.Id,
            context.StepRun.Id,
            context.Assignment.Id);

        return ProcessWorkflowExecutionOutcome.CreateHandled(
            MapWorkflowStateToStepStatus(run.State),
            BuildWorkflowOutcomeReason(run),
            link);
    }

    private async Task<ProcessWorkflowDispatchContext?> LoadWorkflowDispatchContextAsync(
        AppDbContext dbContext,
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == stepRunId && item.ProcessRunId == processRunId, cancellationToken);
        if (run is null || stepRun is null)
        {
            return null;
        }

        var workBriefs = await dbContext.Set<ProcessWorkBrief>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId && item.StepRunId == stepRunId)
            .ToListAsync(cancellationToken);
        var workBrief = workBriefs
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepRun.StepDefinitionId)
            .OrderBy(item => item.FallbackOrder)
            .ToListAsync(cancellationToken);
        var roleIds = stepRoleRequirements
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        if (roleIds.Count == 0)
        {
            return null;
        }

        var assignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == processRunId &&
                roleIds.Contains(item.RoleRequirementId) &&
                (!item.StepDefinitionId.HasValue || item.StepDefinitionId == stepRun.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var assignment = ResolveCurrentAssignment(stepRun, stepRoleRequirements, assignments);
        if (assignment is null)
        {
            return null;
        }

        var role = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assignment.RoleRequirementId, cancellationToken);
        if (!IsWorkflowAssignment(assignment, role))
        {
            return null;
        }

        var expectedArtifacts = await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepRun.StepDefinitionId)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);

        return new ProcessWorkflowDispatchContext(run, stepRun, assignment, role, workBrief, expectedArtifacts);
    }

    private static ProcessRunAssignment? ResolveCurrentAssignment(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> assignments)
    {
        if (assignments.Count == 0)
        {
            return null;
        }

        if (stepRun.CurrentExecutorPartyId.HasValue)
        {
            var partyMatch = assignments
                .Where(item => item.PartyId == stepRun.CurrentExecutorPartyId.Value)
                .OrderByDescending(item => item.StepDefinitionId == stepRun.StepDefinitionId)
                .FirstOrDefault();
            if (partyMatch is not null)
            {
                return partyMatch;
            }
        }

        var rolePriority = stepRoleRequirements
            .Select(
                (requirement, index) => new
                {
                    requirement.RoleRequirementId,
                    Priority = ResolveResponsibilityPriority(requirement.ResponsibilityKind) * 1000 + requirement.FallbackOrder * 10 + index
                })
            .GroupBy(item => item.RoleRequirementId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.Priority));

        return assignments
            .OrderByDescending(item => item.StepDefinitionId == stepRun.StepDefinitionId)
            .ThenByDescending(item => item.PartyId.HasValue || item.WorkflowDefinitionId.HasValue)
            .ThenBy(item => rolePriority.GetValueOrDefault(item.RoleRequirementId, int.MaxValue))
            .FirstOrDefault();
    }

    private static int ResolveResponsibilityPriority(ProcessResponsibilityKind responsibilityKind)
    {
        return responsibilityKind switch
        {
            ProcessResponsibilityKind.Responsible => 0,
            ProcessResponsibilityKind.Accountable => 1,
            ProcessResponsibilityKind.Approver => 2,
            ProcessResponsibilityKind.Reviewer => 3,
            ProcessResponsibilityKind.Backup => 4,
            _ => 5
        };
    }

    private static bool IsWorkflowAssignment(ProcessRunAssignment assignment, ProcessRoleRequirement? role)
    {
        return ProcessExecutorKindNames.IsWorkflow(assignment.ExecutorKind) ||
            assignment.WorkflowDefinitionId.HasValue ||
            ProcessExecutorKindNames.IsWorkflow(role?.PreferredExecutorKind) ||
            role?.PreferredWorkflowDefinitionId.HasValue == true;
    }

    private static bool TryResolveWorkflowReference(
        ProcessRunAssignment assignment,
        ProcessRoleRequirement? role,
        out WorkflowId workflowId,
        out WorkflowVersionId workflowVersionId,
        out string failureReason)
    {
        var workflowDefinitionId = assignment.WorkflowDefinitionId ?? role?.PreferredWorkflowDefinitionId;
        var workflowDefinitionVersionId = assignment.WorkflowVersionId ?? role?.PreferredWorkflowVersionId;
        if (!workflowDefinitionId.HasValue || !workflowDefinitionVersionId.HasValue)
        {
            workflowId = default;
            workflowVersionId = default;
            failureReason = $"Workflow assignment '{assignment.DisplayName}' does not have both workflow definition and version identifiers.";
            return false;
        }

        workflowId = new WorkflowId(workflowDefinitionId.Value);
        workflowVersionId = new WorkflowVersionId(workflowDefinitionVersionId.Value);
        failureReason = string.Empty;
        return true;
    }

    private async Task ProjectWorkflowArtifactsAsync(
        AppDbContext dbContext,
        ProcessWorkflowDispatchContext context,
        ProcessWorkflowRunLink link,
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken)
    {
        var existingExternalReferenceKeys = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == context.Run.Id && !string.IsNullOrWhiteSpace(item.ExternalReferenceKey))
            .Select(item => item.ExternalReferenceKey)
            .ToListAsync(cancellationToken);
        var existingKeys = existingExternalReferenceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runReferenceKey = BuildWorkflowRunExternalReferenceKey(run.RunId);
        if (!existingKeys.Contains(runReferenceKey))
        {
            await dbContext.Set<ProcessArtifactRecord>().AddAsync(
                new ProcessArtifactRecord
                {
                    ProcessRunId = context.Run.Id,
                    StepRunId = context.StepRun.Id,
                    ArtifactKind = ProcessArtifactKind.Transcript,
                    Title = $"Workflow run {run.RunId}",
                    TrustStatus = ResolveWorkflowArtifactTrustStatus(run.State),
                    SensitivityLevel = ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = $"Produced by workflow runtime backend {run.Backend}.",
                    AllowedFutureUsageSummary = "Use as process workflow execution evidence.",
                    ReviewSummary = run.Summary,
                    ManagedStoragePath = string.Empty,
                    ExternalReferenceKey = runReferenceKey,
                    ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                        new ProcessArtifactProjectionLineage
                        {
                            SourceKind = ProcessArtifactProjectionSourceKind.WorkflowRun,
                            WorkflowRunId = run.RunId.Value,
                            SourceExternalReferenceKey = runReferenceKey
                        }),
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            existingKeys.Add(runReferenceKey);
        }

        var workflowArtifacts = await workflowRunStore.ListArtifactsAsync(run.RunId, cancellationToken);
        var expectations = await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == context.StepRun.StepDefinitionId)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        foreach (var artifact in workflowArtifacts)
        {
            var externalReferenceKey = BuildWorkflowArtifactExternalReferenceKey(run.RunId, artifact.Id);
            if (existingKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var artifactKind = WorkflowSubprocessArtifactMapper.MapWorkflowArtifactKind(artifact.Kind);
            var expectation = WorkflowSubprocessArtifactMapper.ResolveWorkflowArtifactExpectation(
                expectations,
                workflowArtifacts,
                artifactKind,
                artifact,
                out var mappingDiagnostic);
            if (!string.IsNullOrWhiteSpace(mappingDiagnostic))
            {
                if (expectation is null)
                {
                    logger.LogWarning(
                        "Workflow artifact {WorkflowArtifactId} from run {WorkflowRunId} was not mapped to a process artifact expectation. Diagnostic: {Diagnostic}",
                        artifact.Id.Value,
                        run.RunId.Value,
                        mappingDiagnostic);
                }
                else
                {
                    logger.LogWarning(
                        "Workflow artifact {WorkflowArtifactId} from run {WorkflowRunId} used compatibility output mapping for process artifact expectation {ArtifactExpectationId}. Diagnostic: {Diagnostic}",
                        artifact.Id.Value,
                        run.RunId.Value,
                        expectation.Id,
                        mappingDiagnostic);
                }
            }

            var isUnmappedAmbiguousArtifact = expectation is null && !string.IsNullOrWhiteSpace(mappingDiagnostic);
            await dbContext.Set<ProcessArtifactRecord>().AddAsync(
                new ProcessArtifactRecord
                {
                    ProcessRunId = context.Run.Id,
                    StepRunId = context.StepRun.Id,
                    ArtifactExpectationId = expectation?.Id,
                    ArtifactKind = expectation?.ArtifactKind ?? (isUnmappedAmbiguousArtifact ? ProcessArtifactKind.Other : artifactKind),
                    Title = expectation?.Title ?? (string.IsNullOrWhiteSpace(artifact.Name)
                        ? $"Workflow artifact {artifact.Id}"
                        : isUnmappedAmbiguousArtifact
                            ? $"Unmapped workflow artifact {artifact.Id}"
                        : artifact.Name),
                    TrustStatus = ResolveWorkflowArtifactTrustStatus(run.State),
                    SensitivityLevel = expectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = $"Produced by workflow run {run.RunId} at node {artifact.NodeId?.Value ?? "workflow"} with workflow artifact id {artifact.Id}.",
                    AllowedFutureUsageSummary = expectation?.AllowedFutureUsageSummary ?? "Use as process workflow output evidence.",
                    ReviewSummary = string.IsNullOrWhiteSpace(mappingDiagnostic)
                        ? artifact.Summary
                        : $"{artifact.Summary} Mapping diagnostic: {mappingDiagnostic}".Trim(),
                    ManagedStoragePath = artifact.StoragePath,
                    ExternalReferenceKey = externalReferenceKey,
                    ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                        new ProcessArtifactProjectionLineage
                        {
                            SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
                            WorkflowRunId = run.RunId.Value,
                            WorkflowArtifactId = artifact.Id.Value,
                            SourceExternalReferenceKey = externalReferenceKey
                        }),
                    CreatedAtUtc = artifact.CreatedAtUtc
                },
                cancellationToken);
        }
    }

    internal static ProcessArtifactExpectation? ResolveWorkflowArtifactExpectation(
        IReadOnlyList<ProcessArtifactExpectation> expectations,
        IReadOnlyList<WorkflowArtifactRecord> workflowArtifacts,
        ProcessArtifactKind artifactKind,
        WorkflowArtifactRecord artifact,
        out string diagnostic)
    {
        return WorkflowSubprocessArtifactMapper.ResolveWorkflowArtifactExpectation(
            expectations,
            workflowArtifacts,
            artifactKind,
            artifact,
            out diagnostic);
    }

    internal static IReadOnlyList<ProcessWorkflowOutputArtifactMapping> ResolveWorkflowOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> expectations)
    {
        return WorkflowSubprocessArtifactMapper.ResolveWorkflowOutputArtifactMappings(expectations);
    }

    private static ProcessArtifactTrustStatus ResolveWorkflowArtifactTrustStatus(WorkflowRunState state)
    {
        return state == WorkflowRunState.Completed
            ? ProcessArtifactTrustStatus.TrustedSource
            : ProcessArtifactTrustStatus.ReviewRequired;
    }

    private static string BuildWorkflowInputJson(ProcessWorkflowDispatchContext context, string trigger)
    {
        return JsonSerializer.Serialize(
            new ProcessWorkflowStartInput(
                context.Run.Id,
                context.StepRun.Id,
                context.Assignment.Id,
                context.Run.Name,
                context.StepRun.Title,
                context.Assignment.DisplayName,
                context.WorkBrief?.WorkBriefText ?? string.Empty,
                context.WorkBrief?.ExpectedOutcome ?? string.Empty,
                trigger,
                WorkflowSubprocessArtifactMapper.ResolveWorkflowOutputArtifactMappings(context.ExpectedArtifacts)
                    .Select(mapping => new ProcessWorkflowOutputMappingInput(
                        mapping.ProcessArtifactExpectationId,
                        mapping.WorkflowOutputId,
                        mapping.WorkflowOutputName,
                        mapping.WorkflowOutputKind))
                    .ToList()),
            JsonOptions);
    }

    private static void UpdateLink(
        ProcessWorkflowRunLink link,
        WorkflowRunSnapshot run,
        DateTimeOffset now)
    {
        link.WorkflowBackend = run.Backend;
        link.WorkflowBackendRunId = run.BackendRunId;
        link.State = run.State;
        link.Summary = run.Summary;
        link.UpdatedAtUtc = now;
    }

    private static ProcessStepRunStatus MapWorkflowStateToStepStatus(WorkflowRunState state)
    {
        return state switch
        {
            WorkflowRunState.Completed => ProcessStepRunStatus.Completed,
            WorkflowRunState.WaitingForInput => ProcessStepRunStatus.WaitingApproval,
            WorkflowRunState.Failed or WorkflowRunState.Cancelled => ProcessStepRunStatus.Failed,
            _ => ProcessStepRunStatus.InProgress
        };
    }

    private static string BuildWorkflowOutcomeReason(WorkflowRunSnapshot run)
    {
        return string.IsNullOrWhiteSpace(run.Summary)
            ? $"Workflow run '{run.RunId}' is {run.State}."
            : run.Summary;
    }

    private static string BuildWorkflowRunExternalReferenceKey(WorkflowRunId runId)
    {
        return $"workflow-run:{runId.Value:D}";
    }

    private static string BuildWorkflowArtifactExternalReferenceKey(WorkflowRunId runId, WorkflowArtifactId artifactId)
    {
        return $"workflow-run:{runId.Value:D}:artifact:{artifactId.Value:D}";
    }

    private static ProcessJournalEntry BuildWorkflowJournalEntry(
        ProcessRun run,
        Guid stepRunId,
        string eventType,
        string title,
        string description,
        string correlationId,
        string replayContextJson,
        DateTimeOffset now)
    {
        return new ProcessJournalEntry
        {
            ProcessRunId = run.Id,
            StepRunId = stepRunId,
            EventType = eventType,
            Title = title,
            Description = description,
            CorrelationId = correlationId,
            OperatingMode = run.OperatingMode,
            PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
            EnvironmentMode = run.OperatingMode.ToString(),
            ReplayContextJson = replayContextJson,
            OccurredAtUtc = now
        };
    }

    private sealed record ProcessWorkflowDispatchContext(
        ProcessRun Run,
        ProcessStepRun StepRun,
        ProcessRunAssignment Assignment,
        ProcessRoleRequirement? Role,
        ProcessWorkBrief? WorkBrief,
        IReadOnlyList<ProcessArtifactExpectation> ExpectedArtifacts);

    private sealed record ProcessWorkflowStartInput(
        Guid ProcessRunId,
        Guid StepRunId,
        Guid AssignmentId,
        string RunName,
        string StepTitle,
        string AssignmentDisplayName,
        string WorkBrief,
        string ExpectedOutcome,
        string Trigger,
        IReadOnlyList<ProcessWorkflowOutputMappingInput> ExpectedOutputMappings);

    private sealed record ProcessWorkflowOutputMappingInput(
        Guid ProcessArtifactExpectationId,
        string WorkflowOutputId,
        string WorkflowOutputName,
        WorkflowArtifactKind? WorkflowOutputKind);
}

internal sealed record ProcessWorkflowExecutionOutcome(
    bool Handled,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    ProcessWorkflowRunLink? Link)
{
    public static ProcessWorkflowExecutionOutcome NotHandled { get; } = new(
        false,
        ProcessStepRunStatus.InProgress,
        string.Empty,
        null);

    public static ProcessWorkflowExecutionOutcome CreateHandled(
        ProcessStepRunStatus completionStatus,
        string completionReason,
        ProcessWorkflowRunLink? link)
    {
        return new ProcessWorkflowExecutionOutcome(true, completionStatus, completionReason, link);
    }
}

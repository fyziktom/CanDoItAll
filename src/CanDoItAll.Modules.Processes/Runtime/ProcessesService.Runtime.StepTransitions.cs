using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const string PendingDecisionRecordSummary = "Decision record required.";

    public async Task<Result> TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);
        try
        {
            var transitionContextResult = await LoadTransitionContextAsync(dbContext, request, cancellationToken);
            if (transitionContextResult.IsFailure)
            {
                return Result.Failure(transitionContextResult.Errors);
            }

            var transitionContext = transitionContextResult.Value!;
            var trimmedReason = request.Reason.Trim();
            var now = clock.GetUtcNow();
            var availableBranchOutcomes = transitionContext.BranchOutcomesByStepId.GetValueOrDefault(transitionContext.StepRun.StepDefinitionId) ?? [];
            var transitionResolutionResult = ProcessStepTransitionGuard.ValidateAndResolve(
                request,
                transitionContext.StepRun,
                transitionContext.CurrentStepDefinition,
                transitionContext.StepDefinitions,
                transitionContext.StepDependenciesByStepId,
                availableBranchOutcomes);
            if (transitionResolutionResult.IsFailure)
            {
                await PersistManualTransitionValidationFailureAsync(
                    dbContext,
                    transitionContext.Run,
                    transitionContext.StepRun,
                    request,
                    transitionResolutionResult.Errors,
                    now,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Failure(transitionResolutionResult.Errors);
            }

            var selectedBranchOutcome = transitionResolutionResult.Value!.SelectedBranchOutcome;
            if (request.TargetStatus == ProcessStepRunStatus.Completed &&
                RequiresArtifactsForCompletedBranch(selectedBranchOutcome))
            {
                var artifactValidationResult = ValidateRequiredArtifactsForCompletion(
                    transitionContext.StepRun,
                    transitionContext.Run,
                    transitionContext.RequiredArtifactExpectations,
                    transitionContext.StepArtifacts,
                    request,
                    CreateProcessArtifactContentReader());
                if (artifactValidationResult.IsFailure)
                {
                    await PersistManualTransitionValidationFailureAsync(
                        dbContext,
                        transitionContext.Run,
                        transitionContext.StepRun,
                        request,
                        artifactValidationResult.Errors,
                        now,
                        cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Result.Failure(artifactValidationResult.Errors);
                }
            }

            var recoveryRoutingDecision = ApplyStepRunTransitionState(transitionContext.StepRun, request, selectedBranchOutcome, trimmedReason, now);

            var stepRunsByDefinitionId = transitionContext.PersistedStepRuns.ToDictionary(item => item.StepDefinitionId);
            stepRunsByDefinitionId[transitionContext.StepRun.StepDefinitionId] = transitionContext.StepRun;

            ProcessRuntimeProgressionPlanner.ApplyTransitionConsequences(
                request.TargetStatus,
                transitionContext.CurrentStepDefinition,
                transitionContext.StepDefinitionsById,
                stepRunsByDefinitionId,
                transitionContext.StepDependenciesByStepId,
                now);

            transitionContext.Run.UpdatedAtUtc = now;
            transitionContext.Run.Status = ProcessRunStatusResolver.Resolve(
                transitionContext.PersistedStepRuns,
                transitionContext.StepRun,
                transitionContext.StepDependenciesByStepId,
                transitionContext.BranchOutcomesByStepId);
            if (transitionContext.Run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled)
            {
                transitionContext.Run.CompletedAtUtc = now;
            }
            else
            {
                transitionContext.Run.CompletedAtUtc = null;
            }

            var decisionKind = request.TargetStatus switch
            {
                ProcessStepRunStatus.Completed when selectedBranchOutcome is not null => ProcessDecisionKind.Variant,
                ProcessStepRunStatus.WaitingApproval => ProcessDecisionKind.Approval,
                ProcessStepRunStatus.Blocked => ProcessDecisionKind.Escalation,
                ProcessStepRunStatus.Refused => ProcessDecisionKind.Refusal,
                ProcessStepRunStatus.Failed => ProcessDecisionKind.Exception,
                _ => ProcessDecisionKind.Assignment
            };
            var decisionOutcome = request.TargetStatus switch
            {
                ProcessStepRunStatus.Blocked => ProcessDecisionOutcome.Escalated,
                ProcessStepRunStatus.Refused => ProcessDecisionOutcome.Refused,
                ProcessStepRunStatus.Completed => ProcessDecisionOutcome.Accepted,
                ProcessStepRunStatus.Failed => ProcessDecisionOutcome.Rejected,
                _ => ProcessDecisionOutcome.Recorded
            };

            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                new ProcessDecisionRecord
                {
                    ProcessRunId = transitionContext.Run.Id,
                    StepRunId = transitionContext.StepRun.Id,
                    DecisionKind = decisionKind,
                    Outcome = decisionOutcome,
                    Title = selectedBranchOutcome is null
                        ? $"{transitionContext.StepRun.Title} -> {request.TargetStatus}"
                        : $"{transitionContext.StepRun.Title} -> {request.TargetStatus} / {selectedBranchOutcome.Title}",
                    Reason = trimmedReason,
                    PolicyEvaluation = transitionContext.StepRun.DecisionSummary,
                    BranchOutcomeId = selectedBranchOutcome?.Id,
                    BranchOutcomeTitle = selectedBranchOutcome?.Title ?? string.Empty,
                    DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? DefaultActor : request.DecidedBy.Trim(),
                    OperatingMode = transitionContext.Run.OperatingMode,
                    CreatedAtUtc = now
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    transitionContext.Run.Id,
                    transitionContext.StepRun.Id,
                    $"step-{request.TargetStatus.ToString().ToLowerInvariant()}",
                    $"Step {request.TargetStatus}",
                    BuildTransitionJournalDescription(transitionContext.StepRun.Title, request.TargetStatus, trimmedReason, selectedBranchOutcome?.Title),
                    transitionContext.Run.OperatingMode,
                    $"definition-version:{transitionContext.Run.ProcessDefinitionVersionId:D}",
                    trimmedReason),
                cancellationToken);

            if (recoveryRoutingDecision is not null)
            {
                await dbContext.Set<ProcessJournalEntry>().AddAsync(
                    BuildRecoveryRoutingJournalEntry(
                        transitionContext.Run,
                        transitionContext.StepRun,
                        recoveryRoutingDecision,
                        trimmedReason,
                        now),
                    cancellationToken);
            }

            if (request.TargetStatus is ProcessStepRunStatus.Blocked or
                ProcessStepRunStatus.Refused or
                ProcessStepRunStatus.Failed or
                ProcessStepRunStatus.WaitingApproval)
            {
                await dbContext.Set<ProcessJournalEntry>().AddAsync(
                    ProcessEscalationJournal.BuildTransitionCreatedEntry(
                        transitionContext.Run,
                        transitionContext.StepRun,
                        request.TargetStatus,
                        trimmedReason,
                        now),
                    cancellationToken);
            }

            if (request.TargetStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed)
            {
                await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                    new ProcessConformanceObservation
                    {
                        ProcessRunId = transitionContext.Run.Id,
                        StepRunId = transitionContext.StepRun.Id,
                        Severity = request.TargetStatus == ProcessStepRunStatus.Failed
                            ? ProcessConformanceSeverity.High
                            : ProcessConformanceSeverity.Moderate,
                        Category = request.TargetStatus.ToString(),
                        Observation = $"{transitionContext.StepRun.Title} resulted in {request.TargetStatus}.",
                        DeviationReason = trimmedReason,
                        IsSafeNonAction = request.TargetStatus == ProcessStepRunStatus.Refused,
                        ContainsSensitiveAssessment = false,
                        CreatedAtUtc = now
                    },
                    cancellationToken);

                await MaybeCreateImprovementCandidateAsync(dbContext, transitionContext.Run, transitionContext.StepRun, request, cancellationToken);
            }

            await projectStructureBridge.SyncRunAsync(
                dbContext,
                transitionContext.Run,
                stepRunsByDefinitionId.Values.ToList(),
                cancellationToken);

            if (!request.SuppressAutomationDispatch)
            {
                await processOutboxService.EnqueueAutomationDispatchAsync(
                    dbContext,
                    transitionContext.Run.ProjectId,
                    transitionContext.Run.ProcessDefinitionId,
                    transitionContext.Run.Id,
                    transitionContext.StepRun.Id,
                    $"step-transition:{request.TargetStatus}",
                    cancellationToken);
            }

            if (ShouldNotifyParentSubprocessStep(transitionContext.Run.Status) &&
                transitionContext.Run.ParentRunId.HasValue &&
                transitionContext.Run.ParentStepRunId.HasValue)
            {
                var parentRun = await dbContext.Set<ProcessRun>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == transitionContext.Run.ParentRunId.Value, cancellationToken);
                if (parentRun is not null)
                {
                    await processOutboxService.EnqueueAutomationDispatchAsync(
                        dbContext,
                        parentRun.ProjectId,
                        parentRun.ProcessDefinitionId,
                        parentRun.Id,
                        transitionContext.Run.ParentStepRunId,
                        $"subprocess-run:{transitionContext.Run.Status}",
                        cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            NotifyRunObservationChanged(
                transitionContext.Run.ProjectId,
                transitionContext.Run.ProcessDefinitionId,
                transitionContext.Run.Id);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStepTransitionConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStepTransitionConflictError());
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(
                Error.Failure(
                    $"Process step transition could not be persisted: {DbUpdateExceptionClassifier.GetProviderMessage(exception)}",
                    "processes.step-transition-persistence-failed"));
        }
    }

    private async Task<Result<ProcessRuntimeTransitionContext>> LoadTransitionContextAsync(
        AppDbContext dbContext,
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .SingleOrDefaultAsync(item => item.Id == request.StepRunId, cancellationToken);
        if (stepRun is null)
        {
            return Result<ProcessRuntimeTransitionContext>.Failure(
                Error.Validation("Process step run was not found.", "processes.step-run-not-found"));
        }

        if (HasConcurrencyTokenMismatch(request.StepRunConcurrencyToken, stepRun.ConcurrencyToken))
        {
            return Result<ProcessRuntimeTransitionContext>.Failure(CreateStepTransitionConflictError());
        }

        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == stepRun.ProcessRunId, cancellationToken);
        if (run.Status == ProcessRunStatus.Cancelled ||
            (run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed &&
                !IsTerminalRunStepTransitionAllowed(run.Status, request, stepRun)))
        {
            return Result<ProcessRuntimeTransitionContext>.Failure(
                Error.Validation(
                    $"Process run '{run.Name}' is {run.Status} and no longer accepts step transitions.",
                    "processes.run-terminal"));
        }

        var stepDefinitions = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToListAsync(cancellationToken);
        var stepIds = stepDefinitions.Select(item => item.Id).ToList();
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> branchOutcomes = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessArtifactExpectation> requiredArtifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => item.StepDefinitionId == stepRun.StepDefinitionId && item.IsRequired)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        var persistedStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == run.Id)
            .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts = (await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == run.Id &&
                item.StepRunId == stepRun.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        var currentStepDefinition = stepDefinitions.Single(item => item.Id == stepRun.StepDefinitionId);
        var branchOutcomesByStepId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var stepDefinitionsById = stepDefinitions.ToDictionary(item => item.Id);
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());

        return Result<ProcessRuntimeTransitionContext>.Success(
            new ProcessRuntimeTransitionContext(
                stepRun,
                run,
                currentStepDefinition,
                stepDefinitions,
                stepDefinitionsById,
                stepDependenciesByStepId,
                branchOutcomesByStepId,
                persistedStepRuns,
                requiredArtifactExpectations,
                stepArtifacts));
    }

    private static bool IsTerminalRunStepRestart(ProcessStepTransitionRequest request, ProcessStepRun stepRun)
    {
        if (request.TargetStatus != ProcessStepRunStatus.InProgress)
        {
            return false;
        }

        if (stepRun.Status is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed)
        {
            return true;
        }

        return request.AllowCompletedAgentRerun &&
            stepRun.Status == ProcessStepRunStatus.Completed;
    }

    private static bool IsTerminalRunStepTransitionAllowed(
        ProcessRunStatus runStatus,
        ProcessStepTransitionRequest request,
        ProcessStepRun stepRun)
    {
        return IsTerminalRunStepRestart(request, stepRun) ||
            runStatus == ProcessRunStatus.Failed && IsFailedRunReopenedStepSettlement(request, stepRun);
    }

    private static bool IsFailedRunReopenedStepSettlement(
        ProcessStepTransitionRequest request,
        ProcessStepRun stepRun)
    {
        return stepRun.Status == ProcessStepRunStatus.InProgress &&
            request.TargetStatus is ProcessStepRunStatus.Completed or
                ProcessStepRunStatus.WaitingApproval or
                ProcessStepRunStatus.Blocked or
                ProcessStepRunStatus.Refused or
                ProcessStepRunStatus.Failed;
    }

    private static bool RequiresArtifactsForCompletedBranch(ProcessStepBranchOutcomeDefinition? selectedBranchOutcome)
    {
        return selectedBranchOutcome is null ||
            !ProcessBranchOutcomeRouting.IsExceptionRoutingBranchOutcome(selectedBranchOutcome);
    }

    private static Result ValidateRequiredArtifactsForCompletion(
        ProcessStepRun stepRun,
        ProcessRun run,
        IReadOnlyList<ProcessArtifactExpectation> requiredArtifactExpectations,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts,
        ProcessStepTransitionRequest request,
        ProcessRunAutomationDispatchService.IProcessArtifactContentReader managedArtifactContentReader)
    {
        if (requiredArtifactExpectations.Count == 0)
        {
            return Result.Success();
        }

        var artifactValidationContext = ResolveArtifactValidationContext(request, stepArtifacts);
        var validationResults = requiredArtifactExpectations
            .Select(expectation => ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                run.Id,
                stepRun.Id,
                ToDispatchArtifactExpectation(expectation),
                stepArtifacts,
                artifactValidationContext.ExecutorKind,
                artifactValidationContext.ExecutionRunId,
                artifactValidationContext.WorkflowRunId,
                artifactValidationContext.SubprocessRunId,
                artifactValidationContext.RecoveryExecutionRunId,
                artifactValidationContext.RecoveredForExecutionRunId,
                managedArtifactContentReader: managedArtifactContentReader))
            .ToArray();
        var failures = validationResults
            .Where(result => !result.IsSatisfied)
            .ToArray();
        if (failures.Length == 0)
        {
            return Result.Success();
        }

        if (failures.All(result => result.Status == ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing))
        {
            return Result.Failure(
                Error.Validation(
                    $"Step '{stepRun.Title}' cannot be completed until the required artifacts are recorded: {string.Join(", ", failures.Select(result => result.ExpectationTitle))}.",
                    "processes.step-completion-missing-required-artifacts"));
        }

        var summary = string.Join(
            "; ",
            failures
                .Take(5)
                .Select(result => $"{result.ExpectationTitle}: {result.Status} ({result.Diagnostic})"));
        return Result.Failure(
            Error.Validation(
                $"Step '{stepRun.Title}' cannot be completed because required artifact contract validation failed: {summary}.",
                "processes.step-completion-invalid-required-artifacts"));
    }

    private static ProcessStepTransitionArtifactValidationContext ResolveArtifactValidationContext(
        ProcessStepTransitionRequest request,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts)
    {
        if (request.ArtifactValidationExecutorKind.HasValue)
        {
            return new ProcessStepTransitionArtifactValidationContext(
                request.ArtifactValidationExecutorKind.Value,
                request.ArtifactValidationExecutionRunId,
                request.ArtifactValidationWorkflowRunId,
                request.ArtifactValidationSubprocessRunId,
                request.ArtifactValidationRecoveryExecutionRunId,
                request.ArtifactValidationRecoveredForExecutionRunId);
        }

        if (IsAutomationDispatchTransition(request) &&
            TryResolveSingleDirectExecutionRunId(stepArtifacts, out var executionRunId))
        {
            return new ProcessStepTransitionArtifactValidationContext(
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                WorkflowRunId: null,
                SubprocessRunId: null,
                RecoveryExecutionRunId: null,
                RecoveredForExecutionRunId: null);
        }

        return new ProcessStepTransitionArtifactValidationContext(
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.Manual,
            ExecutionRunId: null,
            WorkflowRunId: null,
            SubprocessRunId: null,
            RecoveryExecutionRunId: null,
            RecoveredForExecutionRunId: null);
    }

    private static bool IsAutomationDispatchTransition(ProcessStepTransitionRequest request)
        => string.Equals(
            request.DecidedBy,
            ProcessRunAutomationDispatchService.AutomationActor,
            StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveSingleDirectExecutionRunId(
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts,
        out Guid executionRunId)
    {
        var executionRunIds = new HashSet<Guid>();
        foreach (var artifact in stepArtifacts)
        {
            AddDirectExecutionRunIds(artifact, executionRunIds);
            if (executionRunIds.Count > 1)
            {
                executionRunId = Guid.Empty;
                return false;
            }
        }

        executionRunId = executionRunIds.SingleOrDefault();
        return executionRunId != Guid.Empty;
    }

    private static void AddDirectExecutionRunIds(
        ProcessArtifactRecord artifact,
        ISet<Guid> executionRunIds)
    {
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null &&
            IsDirectExecutionLineage(lineage.SourceKind))
        {
            AddGuid(executionRunIds, lineage.SourceExecutionRunId);
            AddGuid(executionRunIds, lineage.ProjectedExecutionRunId);
        }

        if (TryReadPipeDelimitedExecutionRunId(artifact.ExternalReferenceKey, "workspace-written-artifact|", out var externalReferenceExecutionRunId) ||
            TryReadPipeDelimitedExecutionRunId(artifact.ExternalReferenceKey, "existing-managed-artifact|", out externalReferenceExecutionRunId) ||
            TryReadPipeDelimitedExecutionRunId(artifact.ExternalReferenceKey, "assistant-response|", out externalReferenceExecutionRunId) ||
            TryReadColonDelimitedExecutionRunId(artifact.ExternalReferenceKey, "agentframework-browser-artifact:", out externalReferenceExecutionRunId))
        {
            executionRunIds.Add(externalReferenceExecutionRunId);
        }
    }

    private static bool IsDirectExecutionLineage(ProcessArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind is
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact or
            ProcessArtifactProjectionSourceKind.WorkspaceWrite or
            ProcessArtifactProjectionSourceKind.ExistingManagedFile or
            ProcessArtifactProjectionSourceKind.AssistantResponse or
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser;
    }

    private static void AddGuid(ISet<Guid> values, Guid? value)
    {
        if (value.HasValue && value.Value != Guid.Empty)
        {
            values.Add(value.Value);
        }
    }

    private static bool TryReadPipeDelimitedExecutionRunId(
        string value,
        string prefix,
        out Guid executionRunId)
    {
        executionRunId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = value[prefix.Length..];
        var separatorIndex = remainder.IndexOf('|', StringComparison.Ordinal);
        var token = separatorIndex < 0 ? remainder : remainder[..separatorIndex];
        return Guid.TryParse(token, out executionRunId);
    }

    private static bool TryReadColonDelimitedExecutionRunId(
        string value,
        string prefix,
        out Guid executionRunId)
    {
        executionRunId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = value[prefix.Length..];
        var separatorIndex = remainder.IndexOf(':', StringComparison.Ordinal);
        var token = separatorIndex < 0 ? remainder : remainder[..separatorIndex];
        return Guid.TryParse(token, out executionRunId);
    }

    private ProcessRunAutomationDispatchService.IProcessArtifactContentReader CreateProcessArtifactContentReader()
    {
        return new ProcessRunAutomationDispatchService.StorageBackedProcessArtifactContentReader(
            workspacePathResolver,
            storageCatalogService,
            storageDriverRegistry);
    }

    private static ProcessRunAutomationDispatchService.DispatchArtifactExpectation ToDispatchArtifactExpectation(
        ProcessArtifactExpectation expectation)
    {
        return new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            expectation.Id,
            expectation.ArtifactKind,
            expectation.Title,
            expectation.IsRequired,
            expectation.TrustRequirement,
            expectation.SensitivityLevel,
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary);
    }

    private sealed record ProcessStepTransitionArtifactValidationContext(
        ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind ExecutorKind,
        Guid? ExecutionRunId,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        Guid? RecoveryExecutionRunId,
        Guid? RecoveredForExecutionRunId);

    private static bool ShouldNotifyParentSubprocessStep(ProcessRunStatus status)
    {
        return status is ProcessRunStatus.Blocked or
            ProcessRunStatus.Completed or
            ProcessRunStatus.Cancelled or
            ProcessRunStatus.Failed;
    }

    private static async Task PersistManualTransitionValidationFailureAsync(
        AppDbContext dbContext,
        ProcessRun run,
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        IReadOnlyList<Error> errors,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var errorCodes = errors
            .Select(item => item.Code)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        var evidenceKey = ProcessRuntimeInvariantAuditor.BuildManualTransitionValidationFailureEvidenceKey(
            stepRun.Id,
            request.TargetStatus,
            errorCodes);
        var alreadyRecorded = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(item =>
                item.ProcessRunId == run.Id &&
                item.StepRunId == stepRun.Id &&
                item.EventType == ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded &&
                item.CorrelationId == evidenceKey,
                cancellationToken);
        if (alreadyRecorded)
        {
            return;
        }

        var errorSummary = string.Join(
            "; ",
            errors
                .Select(item => string.IsNullOrWhiteSpace(item.Code) ? item.Message : $"{item.Code}: {item.Message}")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(5));
        if (string.IsNullOrWhiteSpace(errorSummary))
        {
            errorSummary = $"Cannot move step from {stepRun.Status} to {request.TargetStatus}.";
        }

        var recommendedAction = ResolveManualTransitionFailureRecommendedAction(errors);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = run.Id,
                StepRunId = stepRun.Id,
                EventType = ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded,
                Title = "Manual transition validation failure",
                Description = errorSummary,
                CorrelationId = evidenceKey,
                OperatingMode = run.OperatingMode,
                PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    RunId = run.Id,
                    StepRunId = stepRun.Id,
                    Code = ProcessRuntimeInvariantAuditor.ManualTransitionValidationFailureCode,
                    Severity = ProcessConformanceSeverity.Moderate.ToString(),
                    CurrentStatus = stepRun.Status.ToString(),
                    TargetStatus = request.TargetStatus.ToString(),
                    ErrorCodes = errorCodes,
                    RecommendedAction = recommendedAction
                }),
                OccurredAtUtc = occurredAtUtc
            },
            cancellationToken);
    }

    private static string ResolveManualTransitionFailureRecommendedAction(IReadOnlyList<Error> errors)
    {
        return errors.Any(item => item.Code.Contains("artifact", StringComparison.OrdinalIgnoreCase))
            ? "Record or repair the required artifacts, refresh the run state, and retry the transition."
            : "Refresh the run state, choose an allowed next status, and retry the transition.";
    }

    private static ProcessRecoveryRoutingDecision? ApplyStepRunTransitionState(
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        ProcessStepBranchOutcomeDefinition? selectedBranchOutcome,
        string trimmedReason,
        DateTimeOffset now)
    {
        var previousStatus = stepRun.Status;
        var previousStartedAtUtc = stepRun.StartedAtUtc;
        ProcessRecoveryRoutingDecision? recoveryRoutingDecision = null;

        if (request.TargetStatus != ProcessStepRunStatus.InProgress &&
            previousStatus == ProcessStepRunStatus.InProgress &&
            previousStartedAtUtc.HasValue)
        {
            stepRun.TouchMinutes += Math.Max(0, (int)(now - previousStartedAtUtc.Value).TotalMinutes);
        }

        if (request.TargetStatus == ProcessStepRunStatus.InProgress)
        {
            stepRun.CompletedAtUtc = null;
            stepRun.BlockedReason = string.Empty;
            ProcessStepRunBlockState.Clear(stepRun);
            stepRun.RefusalReason = string.Empty;
            stepRun.ExceptionSummary = string.Empty;
            if (previousStatus is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval &&
                stepRun.ReadyAtUtc.HasValue)
            {
                stepRun.WaitMinutes += Math.Max(0, (int)(now - stepRun.ReadyAtUtc.Value).TotalMinutes);
            }

            stepRun.StartedAtUtc = now;
        }

        if (request.TargetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed or ProcessStepRunStatus.Skipped)
        {
            stepRun.CompletedAtUtc = now;
        }

        if (request.TargetStatus == ProcessStepRunStatus.Blocked)
        {
            stepRun.BlockedReason = trimmedReason;
            recoveryRoutingDecision = ProcessStepRunBlockState.Apply(stepRun, trimmedReason, request.BlockCause);
            stepRun.BlockedMinutes = Math.Max(stepRun.BlockedMinutes, 15);
        }

        if (request.TargetStatus == ProcessStepRunStatus.Refused)
        {
            stepRun.RefusalReason = trimmedReason;
            stepRun.DecisionSummary = "Safe refusal recorded.";
        }

        if (request.TargetStatus == ProcessStepRunStatus.Failed)
        {
            stepRun.ExceptionSummary = trimmedReason;
            recoveryRoutingDecision = ProcessStepRunBlockState.Apply(stepRun, trimmedReason, request.BlockCause);
            stepRun.ReworkCount += 1;
        }

        if (request.TargetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Skipped or ProcessStepRunStatus.Refused)
        {
            ProcessStepRunBlockState.Clear(stepRun);
        }

        if (selectedBranchOutcome is not null)
        {
            stepRun.SelectedBranchOutcomeId = selectedBranchOutcome.Id;
            stepRun.SelectedBranchOutcomeTitle = selectedBranchOutcome.Title;
            stepRun.DecisionSummary = $"Selected branch outcome: {selectedBranchOutcome.Title}.";
        }

        if (request.TargetStatus == ProcessStepRunStatus.Skipped)
        {
            stepRun.DecisionSummary = string.IsNullOrWhiteSpace(trimmedReason)
                ? "Skipped."
                : trimmedReason;
        }

        if (request.TargetStatus == ProcessStepRunStatus.Completed &&
            selectedBranchOutcome is null &&
            string.Equals(stepRun.DecisionSummary, PendingDecisionRecordSummary, StringComparison.Ordinal))
        {
            stepRun.DecisionSummary = string.IsNullOrWhiteSpace(trimmedReason)
                ? "Decision recorded."
                : trimmedReason;
        }

        stepRun.Status = request.TargetStatus;
        return recoveryRoutingDecision;
    }

    private ProcessJournalEntry BuildRecoveryRoutingJournalEntry(
        ProcessRun run,
        ProcessStepRun stepRun,
        ProcessRecoveryRoutingDecision decision,
        string diagnostic,
        DateTimeOffset occurredAtUtc)
    {
        return new ProcessJournalEntry
        {
            ProcessRunId = run.Id,
            StepRunId = stepRun.Id,
            EventType = ProcessRuntimeEventTypes.RecoveryRoutingDecisionRecorded,
            Title = "Recovery routing decision recorded",
            Description = $"{stepRun.Title}: {decision.NextAction}. {diagnostic}",
            CorrelationId = decision.EvidenceFingerprint,
            OperatingMode = run.OperatingMode,
            PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
            EnvironmentMode = run.OperatingMode.ToString(),
            ReplayContextJson = JsonSerializer.Serialize(new
            {
                RunId = run.Id,
                StepRunId = stepRun.Id,
                BlockReasonCode = decision.BlockReasonCode.ToString(),
                FailureOwnership = decision.FailureOwnership?.ToString() ?? string.Empty,
                NextAction = decision.NextAction.ToString(),
                Classification = decision.Classification.ToString(),
                AvailableActions = decision.AvailableActions.Select(action => action.ToString()).ToArray(),
                decision.EvidenceFingerprint,
                decision.IsNoProgressGuarded,
                decision.Reason,
                Diagnostic = diagnostic
            }),
            OccurredAtUtc = occurredAtUtc
        };
    }

    private sealed record ProcessRuntimeTransitionContext(
        ProcessStepRun StepRun,
        ProcessRun Run,
        ProcessStepDefinition CurrentStepDefinition,
        IReadOnlyList<ProcessStepDefinition> StepDefinitions,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> StepDefinitionsById,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> StepDependenciesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> BranchOutcomesByStepId,
        IReadOnlyList<ProcessStepRun> PersistedStepRuns,
        IReadOnlyList<ProcessArtifactExpectation> RequiredArtifactExpectations,
        IReadOnlyList<ProcessArtifactRecord> StepArtifacts);
}

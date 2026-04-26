using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
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
                return Result.Failure(transitionResolutionResult.Errors);
            }

            var selectedBranchOutcome = transitionResolutionResult.Value!.SelectedBranchOutcome;
            if (request.TargetStatus == ProcessStepRunStatus.Completed)
            {
                var artifactValidationResult = ValidateRequiredArtifactsForCompletion(
                    transitionContext.StepRun,
                    transitionContext.RequiredArtifactExpectations,
                    transitionContext.StepArtifacts);
                if (artifactValidationResult.IsFailure)
                {
                    return Result.Failure(artifactValidationResult.Errors);
                }
            }

            var trimmedReason = request.Reason.Trim();
            var now = clock.GetUtcNow();

            ApplyStepRunTransitionState(transitionContext.StepRun, request, selectedBranchOutcome, trimmedReason, now);

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
            transitionContext.Run.Status = ProcessRunStatusCalculator.Resolve(transitionContext.PersistedStepRuns, transitionContext.StepRun);
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

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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

    private static Result ValidateRequiredArtifactsForCompletion(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessArtifactExpectation> requiredArtifactExpectations,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts)
    {
        if (requiredArtifactExpectations.Count == 0)
        {
            return Result.Success();
        }

        var unmetRequirements = new List<string>();
        foreach (var expectation in requiredArtifactExpectations)
        {
            var artifact = stepArtifacts.FirstOrDefault(item => SatisfiesArtifactExpectation(item, expectation));
            if (artifact is null)
            {
                unmetRequirements.Add(expectation.Title);
            }
        }

        if (unmetRequirements.Count == 0)
        {
            return Result.Success();
        }

        return Result.Failure(
            Error.Validation(
                $"Step '{stepRun.Title}' cannot be completed until the required artifacts are recorded: {string.Join(", ", unmetRequirements)}.",
                "processes.step-completion-missing-required-artifacts"));
    }

    private static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            return false;
        }

        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SatisfiesTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }

    private static void ApplyStepRunTransitionState(
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        ProcessStepBranchOutcomeDefinition? selectedBranchOutcome,
        string trimmedReason,
        DateTimeOffset now)
    {
        var previousStatus = stepRun.Status;
        var previousStartedAtUtc = stepRun.StartedAtUtc;

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
            stepRun.ReworkCount += 1;
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

        stepRun.Status = request.TargetStatus;
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

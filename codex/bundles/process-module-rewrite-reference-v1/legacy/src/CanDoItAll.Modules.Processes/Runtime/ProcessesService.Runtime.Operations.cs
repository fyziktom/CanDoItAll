using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const int MaxProcessArtifactTitleLength = 200;
    private const int MaxProcessArtifactExternalReferenceKeyLength = 200;
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public async Task<Result> ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessRunId == Guid.Empty || request.RoleRequirementId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Run and role are required for assignment resolution.", "processes.assignment.run-role-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure(Error.Validation("Process run was not found.", "processes.assignment.run-not-found"));
        }

        const int maxAssignmentResolutionAttempts = 3;
        for (var attempt = 0; attempt < maxAssignmentResolutionAttempts; attempt++)
        {
            await using var transaction = await BeginAssignmentResolutionTransactionAsync(dbContext, request, cancellationToken);
            var assignment = await dbContext.Set<ProcessRunAssignment>()
                .SingleOrDefaultAsync(
                    item => item.ProcessRunId == request.ProcessRunId &&
                        item.RoleRequirementId == request.RoleRequirementId &&
                        item.StepDefinitionId == request.StepDefinitionId,
                    cancellationToken);
            var createdAssignment = false;
            if (assignment is null)
            {
                assignment = new ProcessRunAssignment
                {
                    ProcessRunId = request.ProcessRunId,
                    RoleRequirementId = request.RoleRequirementId,
                    StepDefinitionId = request.StepDefinitionId
                };
                createdAssignment = true;

                await dbContext.Set<ProcessRunAssignment>().AddAsync(assignment, cancellationToken);
            }

            var executorKind = ProcessExecutorKindNames.Resolve(request.ExecutorKind);
            assignment.PartyId = executorKind == ProcessExecutorKind.Workflow ? null : request.PartyId;
            assignment.WorkflowDefinitionId = executorKind == ProcessExecutorKind.Workflow ? request.WorkflowDefinitionId : null;
            assignment.WorkflowVersionId = executorKind == ProcessExecutorKind.Workflow ? request.WorkflowVersionId : null;
            assignment.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Unassigned role" : request.DisplayName.Trim();
            assignment.ExecutorKind = ProcessExecutorKindNames.ToPersistedName(executorKind);
            assignment.BindingReason = request.BindingReason.Trim();
            assignment.IsFallback = request.IsFallback;
            assignment.IsCapabilityGap = !HasExecutableTarget(assignment) && string.IsNullOrWhiteSpace(request.DisplayName);
            assignment.AllowsDirectMessaging = executorKind != ProcessExecutorKind.Workflow &&
                request.AllowsDirectMessaging &&
                !assignment.IsCapabilityGap;

            await RefreshAffectedStepExecutorSnapshotsAsync(
                dbContext,
                request.ProcessRunId,
                request.StepDefinitionId,
                assignment,
                cancellationToken);

            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                new ProcessDecisionRecord
                {
                    ProcessRunId = request.ProcessRunId,
                    DecisionKind = ProcessDecisionKind.Assignment,
                    Outcome = assignment.IsCapabilityGap ? ProcessDecisionOutcome.Escalated : ProcessDecisionOutcome.Accepted,
                    Title = $"Resolved role assignment {assignment.DisplayName}",
                    Reason = assignment.BindingReason,
                    PolicyEvaluation = assignment.IsFallback ? "Fallback assignment was used." : "Primary assignment was used.",
                    DecidedBy = DefaultActor,
                    OperatingMode = run.OperatingMode,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    request.ProcessRunId,
                    null,
                    "assignment-resolved",
                    "Resolved process assignment",
                    assignment.BindingReason,
                    run.OperatingMode,
                    $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    assignment.DisplayName),
                cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return Result.Success();
            }
            catch (DbUpdateConcurrencyException) when (attempt + 1 < maxAssignmentResolutionAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure(CreateAssignmentConcurrencyConflictError());
            }
            catch (DbUpdateException exception) when (createdAssignment && attempt + 1 < maxAssignmentResolutionAttempts && IsRunAssignmentUniqueConflict(exception))
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (IsRunAssignmentUniqueConflict(exception))
            {
                return Result.Failure(CreateAssignmentUniqueConflictError());
            }
        }

        return Result.Failure(CreateAssignmentUniqueConflictError());
    }

    private static async Task<IDbContextTransaction?> BeginAssignmentResolutionTransactionAsync(
        AppDbContext dbContext,
        ProcessAssignmentResolutionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"{request.ProcessRunId:D}:{request.RoleRequirementId:D}:{request.StepDefinitionId?.ToString("D") ?? "run"}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        return transaction;
    }

    private static async Task RefreshAffectedStepExecutorSnapshotsAsync(
        AppDbContext dbContext,
        Guid processRunId,
        Guid? stepDefinitionId,
        ProcessRunAssignment assignment,
        CancellationToken cancellationToken)
    {
        var stepRunsQuery = dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == processRunId);
        if (stepDefinitionId.HasValue)
        {
            stepRunsQuery = stepRunsQuery.Where(item => item.StepDefinitionId == stepDefinitionId.Value);
        }

        var stepRuns = await stepRunsQuery.ToListAsync(cancellationToken);
        if (stepRuns.Count == 0)
        {
            return;
        }

        var stepDefinitionIds = stepRuns
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var stepDefinitions = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => stepDefinitionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var runAssignments = await dbContext.Set<ProcessRunAssignment>()
            .Where(item =>
                item.ProcessRunId == processRunId &&
                (!item.StepDefinitionId.HasValue || stepDefinitionIds.Contains(item.StepDefinitionId.Value)))
            .ToListAsync(cancellationToken);
        var existingAssignmentIndex = runAssignments.FindIndex(item =>
            item.RoleRequirementId == assignment.RoleRequirementId &&
            item.StepDefinitionId == assignment.StepDefinitionId);
        if (existingAssignmentIndex >= 0)
        {
            runAssignments[existingAssignmentIndex] = assignment;
        }
        else
        {
            runAssignments.Add(assignment);
        }

        var roleRequirementsByStepDefinitionId = BuildStepRoleRequirementsByStepId(stepRoleRequirements);
        foreach (var stepRun in stepRuns)
        {
            if (!stepDefinitions.TryGetValue(stepRun.StepDefinitionId, out var stepDefinition))
            {
                continue;
            }

            var currentStepRoleRequirements = roleRequirementsByStepDefinitionId.GetValueOrDefault(stepRun.StepDefinitionId) ?? [];
            var effectiveAssignmentsByRoleRequirementId = BuildEffectiveAssignmentsByRoleRequirementId(stepDefinition.Id, runAssignments);
            var currentExecutor = ResolveCurrentExecutorAssignment(
                stepDefinition,
                currentStepRoleRequirements,
                effectiveAssignmentsByRoleRequirementId);
            stepRun.CurrentExecutorPartyId = currentExecutor?.PartyId;
            stepRun.CurrentExecutorName = currentExecutor?.DisplayName ?? string.Empty;
            stepRun.CapabilityGapSeverity = ResolveStepCapabilityGapSeverity(
                currentStepRoleRequirements,
                effectiveAssignmentsByRoleRequirementId);
        }
    }

    public async Task<Result<Guid>> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title?.Trim();
        if (request.ProcessRunId == Guid.Empty || string.IsNullOrWhiteSpace(title))
        {
            return Result<Guid>.Failure(Error.Validation("Run and title are required for artifact records.", "processes.artifact.required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null)
        {
            return Result<Guid>.Failure(Error.Validation("Process run was not found.", "processes.artifact.run-not-found"));
        }

        var externalReferenceKey = BoundProcessArtifactText(
            (request.ExternalReferenceKey ?? string.Empty).Trim(),
            MaxProcessArtifactExternalReferenceKeyLength);
        var managedStoragePath = (request.ManagedStoragePath ?? string.Empty).Trim();
        var projectionLineage = CloneProjectionLineage(request.ProjectionLineage);
        if (ShouldComputeManagedArtifactContentHash(projectionLineage) &&
            !string.IsNullOrWhiteSpace(managedStoragePath))
        {
            projectionLineage!.ContentHash = await TryComputeManagedArtifactContentHashAsync(
                managedStoragePath,
                cancellationToken);
        }

        projectionLineage = ProcessArtifactIdentityService.NormalizeProjectionLineage(projectionLineage);
        var projectionIdentityHash = projectionLineage?.ProjectionIdentityHash ?? string.Empty;
        await using var transaction = await BeginArtifactRecordTransactionAsync(
            dbContext,
            request.ProcessRunId,
            projectionIdentityHash,
            externalReferenceKey,
            cancellationToken);

        ProcessStepRun? stepRun = null;
        if (request.StepRunId.HasValue)
        {
            stepRun = await dbContext.Set<ProcessStepRun>()
                .SingleOrDefaultAsync(
                    item => item.Id == request.StepRunId.Value &&
                        item.ProcessRunId == request.ProcessRunId,
                    cancellationToken);
            if (stepRun is null)
            {
                return Result<Guid>.Failure(
                    Error.Validation("Artifact step run was not found for the selected process run.", "processes.artifact.step-run-not-found"));
            }
        }

        ProcessArtifactExpectation? artifactExpectation = null;
        IReadOnlyList<ProcessArtifactExpectation> stepArtifactExpectations = [];
        if (request.ArtifactExpectationId.HasValue)
        {
            artifactExpectation = await dbContext.Set<ProcessArtifactExpectation>()
                .SingleOrDefaultAsync(item => item.Id == request.ArtifactExpectationId.Value, cancellationToken);
            if (artifactExpectation is null)
            {
                return Result<Guid>.Failure(
                    Error.Validation("Artifact expectation was not found.", "processes.artifact.expectation-not-found"));
            }

            if (stepRun is null)
            {
                return Result<Guid>.Failure(
                    Error.Validation("Artifact expectations must be recorded against a concrete step run.", "processes.artifact.expectation-step-required"));
            }

            if (artifactExpectation.StepDefinitionId != stepRun.StepDefinitionId)
            {
                return Result<Guid>.Failure(
                    Error.Validation("Artifact expectation does not belong to the selected step run.", "processes.artifact.expectation-step-mismatch"));
            }
        }
        else if (stepRun is not null)
        {
            stepArtifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => item.StepDefinitionId == stepRun.StepDefinitionId)
                .OrderBy(item => item.Title)
                .ToListAsync(cancellationToken);
            artifactExpectation = ResolveArtifactExpectation(stepArtifactExpectations, request.ArtifactKind, title);
        }

        var scopedArtifactExpectationId = artifactExpectation?.Id ?? request.ArtifactExpectationId;
        if (!string.IsNullOrWhiteSpace(projectionIdentityHash))
        {
            var existingArtifact = await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.ProcessRunId == request.ProcessRunId &&
                    item.ProjectionIdentityHash == projectionIdentityHash)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingArtifact is not null)
            {
                return ResolveExistingArtifactRecordConflict(
                    existingArtifact,
                    request.StepRunId,
                    scopedArtifactExpectationId,
                    "Artifact projection identity is already bound to another step or expectation in this process run.",
                    "processes.artifact.projection-scope-conflict");
            }
        }

        if (!string.IsNullOrWhiteSpace(externalReferenceKey))
        {
            var existingArtifact = await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.ProcessRunId == request.ProcessRunId &&
                    item.ExternalReferenceKey == externalReferenceKey)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingArtifact is not null)
            {
                return ResolveExistingArtifactRecordConflict(
                    existingArtifact,
                    request.StepRunId,
                    scopedArtifactExpectationId,
                    "Artifact external reference is already bound to another step or expectation in this process run.",
                    "processes.artifact.external-reference-scope-conflict");
            }
        }

        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = request.ProcessRunId,
            StepRunId = request.StepRunId,
            ArtifactExpectationId = scopedArtifactExpectationId,
            ArtifactKind = request.ArtifactKind,
            Title = BoundProcessArtifactText(title, MaxProcessArtifactTitleLength),
            TrustStatus = request.TrustStatus,
            SensitivityLevel = request.SensitivityLevel,
            ProvenanceSummary = (request.ProvenanceSummary ?? string.Empty).Trim(),
            AllowedFutureUsageSummary = (request.AllowedFutureUsageSummary ?? string.Empty).Trim(),
            ReviewSummary = (request.ReviewSummary ?? string.Empty).Trim(),
            ManagedStoragePath = managedStoragePath,
            ExternalReferenceKey = externalReferenceKey,
            ProjectionLineageJson = ProcessArtifactIdentityService.SerializeNormalizedProjectionLineage(projectionLineage),
            ProjectionIdentityHash = projectionIdentityHash,
            CreatedAtUtc = clock.GetUtcNow()
        };
        await dbContext.Set<ProcessArtifactRecord>().AddAsync(artifact, cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                request.ProcessRunId,
                request.StepRunId,
                "artifact-recorded",
                "Recorded process artifact",
                artifact.Title,
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                artifact.ManagedStoragePath),
            cancellationToken);
        if (artifactExpectation is not null)
        {
            await ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync(
                dbContext,
                run,
                artifactExpectation,
                artifact,
                cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            var resolvedConflict = await ResolveArtifactRecordUniqueConflictAsync(
                dbContext,
                request.ProcessRunId,
                request.StepRunId,
                scopedArtifactExpectationId,
                projectionIdentityHash,
                externalReferenceKey,
                cancellationToken);
            if (resolvedConflict is not null)
            {
                return resolvedConflict;
            }

            logger.LogWarning(
                exception,
                "Process artifact uniqueness conflict could not be resolved for run {RunId}, step {StepRunId}, has projection identity {HasProjectionIdentity}, has external reference {HasExternalReference}.",
                request.ProcessRunId,
                request.StepRunId,
                !string.IsNullOrWhiteSpace(projectionIdentityHash),
                !string.IsNullOrWhiteSpace(externalReferenceKey));
            return Result<Guid>.Failure(Error.Validation(
                "Artifact projection conflicted with a concurrently recorded artifact.",
                "processes.artifact.unique-conflict"));
        }

        NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);
        return Result<Guid>.Success(artifact.Id);
    }

    private static async Task<IDbContextTransaction?> BeginArtifactRecordTransactionAsync(
        AppDbContext dbContext,
        Guid processRunId,
        string projectionIdentityHash,
        string externalReferenceKey,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        var lockKey = ResolveArtifactRecordLockKey(processRunId, projectionIdentityHash, externalReferenceKey);
        if (string.IsNullOrWhiteSpace(lockKey))
        {
            return null;
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        return transaction;
    }

    private static string ResolveArtifactRecordLockKey(
        Guid processRunId,
        string projectionIdentityHash,
        string externalReferenceKey)
    {
        if (!string.IsNullOrWhiteSpace(projectionIdentityHash))
        {
            return $"{processRunId:D}:projection:{projectionIdentityHash.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(externalReferenceKey))
        {
            return $"{processRunId:D}:external:{externalReferenceKey.Trim()}";
        }

        return string.Empty;
    }

    private static async Task<Result<Guid>?> ResolveArtifactRecordUniqueConflictAsync(
        AppDbContext dbContext,
        Guid processRunId,
        Guid? stepRunId,
        Guid? artifactExpectationId,
        string projectionIdentityHash,
        string externalReferenceKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(projectionIdentityHash))
        {
            var existingArtifact = await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.ProcessRunId == processRunId &&
                    item.ProjectionIdentityHash == projectionIdentityHash)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingArtifact is not null)
            {
                return ResolveExistingArtifactRecordConflict(
                    existingArtifact,
                    stepRunId,
                    artifactExpectationId,
                    "Artifact projection identity is already bound to another step or expectation in this process run.",
                    "processes.artifact.projection-scope-conflict");
            }
        }

        if (!string.IsNullOrWhiteSpace(externalReferenceKey))
        {
            var existingArtifact = await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.ProcessRunId == processRunId &&
                    item.ExternalReferenceKey == externalReferenceKey)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingArtifact is not null)
            {
                return ResolveExistingArtifactRecordConflict(
                    existingArtifact,
                    stepRunId,
                    artifactExpectationId,
                    "Artifact external reference is already bound to another step or expectation in this process run.",
                    "processes.artifact.external-reference-scope-conflict");
            }
        }

        return null;
    }

    private static Result<Guid> ResolveExistingArtifactRecordConflict(
        ProcessArtifactRecord existingArtifact,
        Guid? stepRunId,
        Guid? artifactExpectationId,
        string scopeConflictMessage,
        string scopeConflictCode)
    {
        return existingArtifact.StepRunId == stepRunId &&
            existingArtifact.ArtifactExpectationId == artifactExpectationId
                ? Result<Guid>.Success(existingArtifact.Id)
                : Result<Guid>.Failure(Error.Validation(scopeConflictMessage, scopeConflictCode));
    }

    private async Task ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync(
        AppDbContext dbContext,
        ProcessRun run,
        ProcessArtifactExpectation artifactExpectation,
        ProcessArtifactRecord materializedArtifact,
        CancellationToken cancellationToken)
    {
        var consumingInputs = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(input => input.ArtifactExpectationId == artifactExpectation.Id)
            .ToListAsync(cancellationToken);
        if (consumingInputs.Count == 0)
        {
            return;
        }

        var consumingStepDefinitionIds = consumingInputs
            .Select(input => input.StepDefinitionId)
            .Distinct()
            .ToList();
        var blockedStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun =>
                stepRun.ProcessRunId == run.Id &&
                consumingStepDefinitionIds.Contains(stepRun.StepDefinitionId) &&
                stepRun.Status == ProcessStepRunStatus.Blocked)
            .ToListAsync(cancellationToken);
        blockedStepRuns = blockedStepRuns
            .Where(ProcessStepRunBlockState.IsMissingUpstreamArtifactBlock)
            .ToList();
        if (blockedStepRuns.Count == 0)
        {
            return;
        }

        var stepDefinitionIds = await dbContext.Set<ProcessStepDefinition>()
            .Where(step => step.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .Select(step => step.Id)
            .ToListAsync(cancellationToken);
        var stepDefinitionsById = await dbContext.Set<ProcessStepDefinition>()
            .Where(step => step.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToDictionaryAsync(step => step.Id, cancellationToken);
        var stepRunsByDefinitionId = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun => stepRun.ProcessRunId == run.Id)
            .ToDictionaryAsync(stepRun => stepRun.StepDefinitionId, cancellationToken);
        var dependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(dependency => stepDefinitionIds.Contains(dependency.StepDefinitionId))
            .OrderBy(dependency => dependency.DisplayOrder)
            .ToListAsync(cancellationToken);
        var dependenciesByStepId = dependencies
            .GroupBy(dependency => dependency.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var artifactInputsByStepId = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(input => consumingStepDefinitionIds.Contains(input.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactExpectationIds = artifactInputsByStepId
            .Select(input => input.ArtifactExpectationId)
            .Distinct()
            .ToList();
        var sourceExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(expectation => artifactExpectationIds.Contains(expectation.Id))
            .ToDictionaryAsync(expectation => expectation.Id, cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(artifact => artifact.ProcessRunId == run.Id && artifact.ArtifactExpectationId.HasValue)
            .ToListAsync(cancellationToken);
        if (materializedArtifact.ProcessRunId == run.Id &&
            materializedArtifact.ArtifactExpectationId.HasValue &&
            artifacts.All(artifact => artifact.Id != materializedArtifact.Id))
        {
            artifacts.Add(materializedArtifact);
        }

        var now = clock.GetUtcNow();

        foreach (var blockedStepRun in blockedStepRuns)
        {
            if (!stepDefinitionsById.TryGetValue(blockedStepRun.StepDefinitionId, out var stepDefinition) ||
                !AreDependenciesSatisfiedForMaterializationResume(stepDefinition, stepRunsByDefinitionId, dependenciesByStepId) ||
                !AreArtifactInputsSatisfiedForMaterializationResume(
                    blockedStepRun.StepDefinitionId,
                    artifactInputsByStepId,
                    sourceExpectations,
                    stepRunsByDefinitionId,
                    artifacts))
            {
                continue;
            }

            ProcessRuntimeProgressionPlanner.ReactivateBlockedStepRunAfterUpstreamArtifactMaterialization(
                blockedStepRun,
                stepDefinition,
                now);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = run.Id,
                    StepRunId = blockedStepRun.Id,
                    EventType = ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationResolved,
                    Title = "Missing upstream artifact materialization resolved",
                    Description = $"Required upstream artifact '{artifactExpectation.Title}' is now recorded; step '{blockedStepRun.Title}' was reopened for dispatch.",
                    CorrelationId = $"{artifactExpectation.Id:D}:{blockedStepRun.Id:D}",
                    OperatingMode = run.OperatingMode,
                    PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = run.OperatingMode.ToString(),
                    ReplayContextJson = "{}",
                    OccurredAtUtc = now
                },
                cancellationToken);
        }
    }

    private static bool AreDependenciesSatisfiedForMaterializationResume(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId)
    {
        foreach (var dependency in ProcessStepDependencyCollection.GetPersistedDependencies(stepDefinition.Id, stepDependenciesByStepId))
        {
            if (!stepRunsByDefinitionId.TryGetValue(dependency.DependsOnStepId, out var sourceStepRun) ||
                sourceStepRun.Status != ProcessStepRunStatus.Completed)
            {
                return false;
            }

            if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStepRun.SelectedBranchOutcomeId != dependency.DependsOnBranchOutcomeId)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreArtifactInputsSatisfiedForMaterializationResume(
        Guid stepDefinitionId,
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> sourceExpectations,
        IReadOnlyDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var input in artifactInputs.Where(input => input.StepDefinitionId == stepDefinitionId))
        {
            if (!sourceExpectations.TryGetValue(input.ArtifactExpectationId, out var sourceExpectation) ||
                !stepRunsByDefinitionId.TryGetValue(sourceExpectation.StepDefinitionId, out var sourceStepRun))
            {
                return false;
            }

            if (!artifacts.Any(artifact =>
                    artifact.StepRunId == sourceStepRun.Id &&
                    artifact.ArtifactExpectationId == input.ArtifactExpectationId))
            {
                return false;
            }
        }

        return true;
    }

    private static string BoundProcessArtifactText(string value, int maxLength)
    {
        var normalized = value.Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)).AsSpan(0, 12)).ToLowerInvariant();
        var prefixLength = Math.Max(0, maxLength - hash.Length - 1);
        return $"{normalized[..prefixLength]}#{hash}";
    }

    private static ProcessArtifactProjectionLineage? CloneProjectionLineage(ProcessArtifactProjectionLineage? lineage)
    {
        return lineage is null
            ? null
            : new ProcessArtifactProjectionLineage
            {
                SourceKind = lineage.SourceKind,
                SourceExecutionRunId = lineage.SourceExecutionRunId,
                RecoveryExecutionRunId = lineage.RecoveryExecutionRunId,
                RecoveredForExecutionRunId = lineage.RecoveredForExecutionRunId,
                ProjectedExecutionRunId = lineage.ProjectedExecutionRunId,
                WorkflowRunId = lineage.WorkflowRunId,
                WorkflowArtifactId = lineage.WorkflowArtifactId,
                SubprocessRunId = lineage.SubprocessRunId,
                SourceArtifactId = lineage.SourceArtifactId,
                ReworkPacketId = lineage.ReworkPacketId,
                SourceExternalReferenceKey = lineage.SourceExternalReferenceKey,
                ContentHash = lineage.ContentHash,
                ProjectionIdentityHash = lineage.ProjectionIdentityHash
            };
    }

    private static bool ShouldComputeManagedArtifactContentHash(ProcessArtifactProjectionLineage? lineage)
    {
        return lineage is not null &&
               string.IsNullOrWhiteSpace(lineage.ContentHash) &&
               lineage.SourceKind is
                   ProcessArtifactProjectionSourceKind.AgentExecutionArtifact or
                   ProcessArtifactProjectionSourceKind.WorkspaceWrite or
                   ProcessArtifactProjectionSourceKind.ExistingManagedFile or
                   ProcessArtifactProjectionSourceKind.ProviderNativeBrowser;
    }

    private async Task<string> TryComputeManagedArtifactContentHashAsync(
        string managedStoragePath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (StorageJson.TryParseReference(managedStoragePath, out var reference) && reference is not null)
            {
                await using var storageStream = await TryOpenManagedStorageReadStreamAsync(reference, cancellationToken);
                return storageStream is null
                    ? string.Empty
                    : await ComputeStreamContentHashAsync(storageStream, cancellationToken);
            }

            if (!TryResolveWorkspaceManagedArtifactPath(managedStoragePath, out var fullPath) ||
                !File.Exists(fullPath))
            {
                return string.Empty;
            }

            await using var fileStream = File.OpenRead(fullPath);
            return await ComputeStreamContentHashAsync(fileStream, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "Could not compute process artifact content hash for managed path {ManagedStoragePath}. Validation will report content availability separately if this artifact is required.",
                BoundProcessArtifactText(managedStoragePath, 160));
            return string.Empty;
        }
    }

    private async Task<Stream?> TryOpenManagedStorageReadStreamAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken)
    {
        if (!reference.StorageId.HasValue)
        {
            if (!TryResolveWorkspaceManagedArtifactPath(reference.Locator, out var fullPath) ||
                !File.Exists(fullPath))
            {
                return null;
            }

            return File.OpenRead(fullPath);
        }

        var storage = await storageCatalogService.GetAsync(reference.StorageId.Value, cancellationToken);
        if (storage is null)
        {
            return null;
        }

        var driver = storageDriverRegistry.Resolve(storage.ProviderKind);
        return await driver.OpenReadAsync(storage, reference, cancellationToken);
    }

    private bool TryResolveWorkspaceManagedArtifactPath(string managedStoragePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(managedStoragePath))
        {
            return false;
        }

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var candidateFullPath = Path.IsPathRooted(managedStoragePath)
            ? Path.GetFullPath(managedStoragePath)
            : Path.GetFullPath(Path.Combine(
                workspaceRoot,
                WorkspaceScopeDescriptor.NormalizeRelativePath(managedStoragePath).Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(workspaceRoot, candidateFullPath))
        {
            return false;
        }

        fullPath = candidateFullPath;
        return true;
    }

    private static async Task<string> ComputeStreamContentHashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return ProcessArtifactIdentityService.ComputeContentHash(memory.ToArray());
    }

    private static bool IsWithinRoot(string root, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(fullPath);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessArtifactExpectation? ResolveArtifactExpectation(
        IReadOnlyList<ProcessArtifactExpectation> expectations,
        ProcessArtifactKind artifactKind,
        string title)
    {
        if (expectations.Count == 0)
        {
            return null;
        }

        var normalizedTitle = title.Trim();
        var matchingKind = expectations
            .Where(item => item.ArtifactKind == artifactKind)
            .ToList();
        if (matchingKind.Count == 0)
        {
            return null;
        }

        var exactMatch = matchingKind.FirstOrDefault(item =>
            string.Equals(item.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var overlappingMatches = matchingKind
            .Where(item => ArtifactTitlesOverlap(item.Title, normalizedTitle))
            .ToList();
        if (overlappingMatches.Count == 1)
        {
            return overlappingMatches[0];
        }

        var requiredMatches = matchingKind
            .Where(item => item.IsRequired)
            .ToList();
        return requiredMatches.Count == 1
            ? requiredMatches[0]
            : null;
    }

    private static bool ArtifactTitlesOverlap(string left, string right)
    {
        var normalizedLeft = NormalizeArtifactTitle(left);
        var normalizedRight = NormalizeArtifactTitle(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal) ||
               normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal);
    }

    private static string NormalizeArtifactTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    public async Task<ProcessImportExportEnvelope> ExportAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return new ProcessImportExportEnvelope
        {
            Definition = ProcessDependencyCompatibilityBridge.ToImportExportModel(
                await GetEditorAsync(definitionId, null, cancellationToken)),
            Warnings = [],
            SourceFormat = "CanDoItAll.ProcessDefinition/v2"
        };
    }

    public async Task<Result> RecordManagerDirectiveAsync(
        ProcessManagerDirectiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProcessRunId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select a process run before instructing its manager.", "processes.manager-directive-run-required"));
        }

        var directive = request.Directive.Trim();
        if (string.IsNullOrWhiteSpace(directive))
        {
            return Result.Failure(Error.Validation("Manager directive cannot be empty.", "processes.manager-directive-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure(Error.Validation("Process run was not found.", "processes.manager-directive-run-not-found"));
        }

        var directiveActor = ResolveManagerDirectiveActor(run.ManagerAgentName, request.InstructedBy);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                run.Id,
                null,
                ProcessRuntimeEventTypes.ManagerDirectiveRecorded,
                "Manager directive recorded",
                directive,
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                directiveActor),
            cancellationToken);
        run.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);

        return Result.Success();
    }

    private static string ResolveManagerDirectiveActor(string managerAgentName, string instructedBy)
    {
        if (!string.IsNullOrWhiteSpace(managerAgentName))
        {
            return managerAgentName;
        }

        if (!string.IsNullOrWhiteSpace(instructedBy))
        {
            return instructedBy.Trim();
        }

        return "process-workspace";
    }

    public async Task<Result<Guid>> ImportAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var importMetadata = new ProcessImportMetadata(
            envelope.SourceFormat,
            string.Join(Environment.NewLine, envelope.Warnings));
        var editor = ProcessDependencyCompatibilityBridge.ToEditorModel(envelope.Definition);
        PrepareImportedDefinitionForSave(editor);
        var subprocessResolution = await ResolveImportedSubprocessReferencesAsync(editor, cancellationToken);
        if (subprocessResolution.IsFailure)
        {
            return Result<Guid>.Failure(subprocessResolution.Errors);
        }

        return await SaveAsync(editor, importMetadata, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await executorRegistryBridge.ListOptionsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessWorkflowDefinitionOption>> ListWorkflowDefinitionOptionsAsync(CancellationToken cancellationToken = default)
    {
        return (await workflowCatalogService.ListDefinitionsAsync(cancellationToken))
            .Where(item => item.Status != WorkflowLifecycleStatus.Archived)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProcessWorkflowDefinitionOption(
                item.Id.Value,
                item.VersionId.Value,
                item.Name,
                item.Status,
                item.PreferredBackend))
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessManagerAgentOption>> ListManagerAgentOptionsAsync(CancellationToken cancellationToken = default)
    {
        var agents = await aiAgentService.ListAgentDirectoryAsync(cancellationToken);
        return agents
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProcessManagerAgentOption(
                item.PartyId,
                item.TechnicalAgentId,
                item.DisplayName,
                item.ProviderName,
                item.DefaultModel,
                item.BindingSummary))
            .ToList();
    }
}

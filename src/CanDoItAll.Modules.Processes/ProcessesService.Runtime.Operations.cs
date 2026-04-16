using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
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

        for (var attempt = 0; attempt < 2; attempt++)
        {
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

            assignment.PartyId = request.PartyId;
            assignment.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Unassigned role" : request.DisplayName.Trim();
            assignment.ExecutorKind = request.ExecutorKind.Trim();
            assignment.BindingReason = request.BindingReason.Trim();
            assignment.IsFallback = request.IsFallback;
            assignment.IsCapabilityGap = !request.PartyId.HasValue && string.IsNullOrWhiteSpace(request.DisplayName);
            assignment.AllowsDirectMessaging = request.AllowsDirectMessaging && !assignment.IsCapabilityGap;

            if (request.StepDefinitionId.HasValue)
            {
                var stepRun = await dbContext.Set<ProcessStepRun>()
                    .SingleOrDefaultAsync(
                        item => item.ProcessRunId == request.ProcessRunId &&
                            item.StepDefinitionId == request.StepDefinitionId.Value,
                        cancellationToken);
                var stepDefinition = await dbContext.Set<ProcessStepDefinition>()
                    .SingleOrDefaultAsync(item => item.Id == request.StepDefinitionId.Value, cancellationToken);
                if (stepRun is not null && stepDefinition is not null)
                {
                    var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                        .Where(item => item.StepDefinitionId == request.StepDefinitionId.Value)
                        .ToListAsync(cancellationToken);
                    var stepAssignments = await dbContext.Set<ProcessRunAssignment>()
                        .Where(item =>
                            item.ProcessRunId == request.ProcessRunId &&
                            (!item.StepDefinitionId.HasValue || item.StepDefinitionId == request.StepDefinitionId.Value))
                        .ToListAsync(cancellationToken);
                    var existingAssignmentIndex = stepAssignments.FindIndex(item =>
                        item.RoleRequirementId == assignment.RoleRequirementId &&
                        item.StepDefinitionId == assignment.StepDefinitionId);
                    if (existingAssignmentIndex >= 0)
                    {
                        stepAssignments[existingAssignmentIndex] = assignment;
                    }
                    else
                    {
                        stepAssignments.Add(assignment);
                    }

                    var currentExecutor = ResolveCurrentExecutorAssignment(stepDefinition, stepRoleRequirements, stepAssignments);
                    stepRun.CurrentExecutorPartyId = currentExecutor?.PartyId;
                    stepRun.CurrentExecutorName = currentExecutor?.DisplayName ?? string.Empty;
                    stepRun.CapabilityGapSeverity = ResolveStepCapabilityGapSeverity(stepDefinition, stepRoleRequirements, stepAssignments);
                }
            }

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
                return Result.Success();
            }
            catch (DbUpdateException exception) when (createdAssignment && attempt == 0 && IsRunAssignmentUniqueConflict(exception))
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

    public async Task<Result<Guid>> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessRunId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title))
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

        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = request.ProcessRunId,
            StepRunId = request.StepRunId,
            ArtifactExpectationId = request.ArtifactExpectationId,
            ArtifactKind = request.ArtifactKind,
            Title = request.Title.Trim(),
            TrustStatus = request.TrustStatus,
            SensitivityLevel = request.SensitivityLevel,
            ProvenanceSummary = request.ProvenanceSummary.Trim(),
            AllowedFutureUsageSummary = request.AllowedFutureUsageSummary.Trim(),
            ReviewSummary = request.ReviewSummary.Trim(),
            ManagedStoragePath = request.ManagedStoragePath.Trim(),
            ExternalReferenceKey = request.ExternalReferenceKey.Trim(),
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
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(artifact.Id);
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

    public async Task<Result<Guid>> ImportAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var importMetadata = new ProcessImportMetadata(
            envelope.SourceFormat,
            string.Join(Environment.NewLine, envelope.Warnings));
        var editor = ProcessDependencyCompatibilityBridge.ToEditorModel(envelope.Definition);
        PrepareImportedDefinitionForSave(editor);
        return await SaveAsync(editor, importMetadata, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await executorRegistryBridge.ListOptionsAsync(cancellationToken);
    }
}

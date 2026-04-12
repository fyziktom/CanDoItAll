using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result> ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessRunId == Guid.Empty || request.RoleRequirementId == Guid.Empty) {
            return Result.Failure(Error.Validation("Run and role are required for assignment resolution.", "processes.assignment.run-role-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null) {
            return Result.Failure(Error.Validation("Process run was not found.", "processes.assignment.run-not-found"));
        }

        var assignment = await dbContext.Set<ProcessRunAssignment>()
            .FirstOrDefaultAsync(
                item => item.ProcessRunId == request.ProcessRunId &&
                    item.RoleRequirementId == request.RoleRequirementId &&
                    item.StepDefinitionId == request.StepDefinitionId,
                cancellationToken);
        if (assignment is null) {
            assignment = new ProcessRunAssignment {
                ProcessRunId = request.ProcessRunId,
                RoleRequirementId = request.RoleRequirementId,
                StepDefinitionId = request.StepDefinitionId
            };

            await dbContext.Set<ProcessRunAssignment>().AddAsync(assignment, cancellationToken);
        }

        assignment.PartyId = request.PartyId;
        assignment.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Unassigned role" : request.DisplayName.Trim();
        assignment.ExecutorKind = request.ExecutorKind.Trim();
        assignment.BindingReason = request.BindingReason.Trim();
        assignment.IsFallback = request.IsFallback;
        assignment.IsCapabilityGap = !request.PartyId.HasValue && string.IsNullOrWhiteSpace(request.DisplayName);

        if (request.StepDefinitionId.HasValue) {
            var stepRuns = await dbContext.Set<ProcessStepRun>()
                .Where(item => item.ProcessRunId == request.ProcessRunId && item.StepDefinitionId == request.StepDefinitionId.Value)
                .ToListAsync(cancellationToken);
            foreach (var stepRun in stepRuns) {
                stepRun.CurrentExecutorPartyId = request.PartyId;
                stepRun.CurrentExecutorName = assignment.DisplayName;
                stepRun.CapabilityGapSeverity = assignment.IsCapabilityGap
                    ? ProcessCapabilityGapSeverity.Attention
                    : ProcessCapabilityGapSeverity.None;
            }
        }

        await dbContext.Set<ProcessDecisionRecord>().AddAsync(
            new ProcessDecisionRecord {
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessRunId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title)) {
            return Result<Guid>.Failure(Error.Validation("Run and title are required for artifact records.", "processes.artifact.required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null) {
            return Result<Guid>.Failure(Error.Validation("Process run was not found.", "processes.artifact.run-not-found"));
        }

        var artifact = new ProcessArtifactRecord {
            ProcessRunId = request.ProcessRunId,
            StepRunId = request.StepRunId,
            ArtifactKind = request.ArtifactKind,
            Title = request.Title.Trim(),
            TrustStatus = request.TrustStatus,
            SensitivityLevel = request.SensitivityLevel,
            ProvenanceSummary = request.ProvenanceSummary.Trim(),
            AllowedFutureUsageSummary = request.AllowedFutureUsageSummary.Trim(),
            ReviewSummary = request.ReviewSummary.Trim(),
            ManagedStoragePath = request.ManagedStoragePath.Trim(),
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

    public async Task<ProcessImportExportEnvelope> ExportAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        return new ProcessImportExportEnvelope {
            Definition = await GetEditorAsync(definitionId, null, cancellationToken),
            Warnings = [],
            SourceFormat = "CanDoItAll.ProcessDefinition/v1"
        };
    }

    public async Task<Result<Guid>> ImportAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default) {
        var importMetadata = new ProcessImportMetadata(
            envelope.SourceFormat,
            string.Join(Environment.NewLine, envelope.Warnings));
        return await SaveAsync(envelope.Definition, importMetadata, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default) {
        return await executorRegistryBridge.ListOptionsAsync(cancellationToken);
    }
}

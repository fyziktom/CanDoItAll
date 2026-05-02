using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const int MaxProcessArtifactTitleLength = 200;
    private const int MaxProcessArtifactExternalReferenceKeyLength = 200;

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

        var roleRequirementsByStepDefinitionId = stepRoleRequirements
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        foreach (var stepRun in stepRuns)
        {
            if (!stepDefinitions.TryGetValue(stepRun.StepDefinitionId, out var stepDefinition))
            {
                continue;
            }

            var currentStepRoleRequirements = roleRequirementsByStepDefinitionId.GetValueOrDefault(stepRun.StepDefinitionId) ?? [];
            var currentExecutor = ResolveCurrentExecutorAssignment(stepDefinition, currentStepRoleRequirements, runAssignments);
            stepRun.CurrentExecutorPartyId = currentExecutor?.PartyId;
            stepRun.CurrentExecutorName = currentExecutor?.DisplayName ?? string.Empty;
            stepRun.CapabilityGapSeverity = ResolveStepCapabilityGapSeverity(stepDefinition, currentStepRoleRequirements, runAssignments);
        }
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

        var externalReferenceKey = BoundProcessArtifactText(
            request.ExternalReferenceKey.Trim(),
            MaxProcessArtifactExternalReferenceKeyLength);
        if (!string.IsNullOrWhiteSpace(externalReferenceKey))
        {
            var existingArtifactId = await dbContext.Set<ProcessArtifactRecord>()
                .Where(item =>
                    item.ProcessRunId == request.ProcessRunId &&
                    item.ExternalReferenceKey == externalReferenceKey)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingArtifactId.HasValue)
            {
                return Result<Guid>.Success(existingArtifactId.Value);
            }
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
            artifactExpectation = ResolveArtifactExpectation(stepArtifactExpectations, request.ArtifactKind, request.Title);
        }

        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = request.ProcessRunId,
            StepRunId = request.StepRunId,
            ArtifactExpectationId = artifactExpectation?.Id ?? request.ArtifactExpectationId,
            ArtifactKind = request.ArtifactKind,
            Title = BoundProcessArtifactText(request.Title.Trim(), MaxProcessArtifactTitleLength),
            TrustStatus = request.TrustStatus,
            SensitivityLevel = request.SensitivityLevel,
            ProvenanceSummary = request.ProvenanceSummary.Trim(),
            AllowedFutureUsageSummary = request.AllowedFutureUsageSummary.Trim(),
            ReviewSummary = request.ReviewSummary.Trim(),
            ManagedStoragePath = request.ManagedStoragePath.Trim(),
            ExternalReferenceKey = externalReferenceKey,
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

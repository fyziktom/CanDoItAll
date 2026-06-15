using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<IReadOnlyList<ProcessArtifactExpectationValidationResult>> ValidateRequiredCompletionArtifactsAsync(
        ProcessStepCompletionFinalizerContext context,
        CancellationToken cancellationToken)
    {
        var candidate = context.Candidate;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == candidate.Run.Id && item.StepRunId == candidate.StepRun.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var managedArtifactContentReader = new StorageBackedProcessArtifactContentReader(
            workspacePathResolver,
            storageCatalogService,
            storageDriverRegistry);
        var results = candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired)
            .Select(expectation => ValidateArtifactExpectationForRecordedArtifacts(
                candidate.Run.Id,
                candidate.StepRun.Id,
                expectation,
                artifacts,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.ExecutorKind == ProcessStepCompletionExecutorKind.WorkflowBackedRole
                    ? context.WorkflowRunId ?? ResolveWorkflowRunIdForStep(artifacts)
                    : null,
                context.ExecutorKind == ProcessStepCompletionExecutorKind.SubprocessParent
                    ? context.SubprocessRunId ?? ResolveSubprocessRunIdForStep(artifacts)
                    : null,
                context.RecoveryExecutionRunId,
                context.RecoveredForExecutionRunId,
                managedArtifactContentReader))
            .ToList();

        candidate.ExternalReferenceKeys.Clear();
        foreach (var externalReferenceKey in artifacts
                     .Select(item => item.ExternalReferenceKey)
                     .Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
        }

        RefreshCandidateArtifactSatisfaction(candidate, results);
        return results;
    }

    private static void RefreshCandidateArtifactSatisfaction(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults)
    {
        candidate.RecordedArtifactExpectationIds.Clear();
        foreach (var result in validationResults.Where(result => result.IsSatisfied))
        {
            candidate.RecordedArtifactExpectationIds.Add(result.ExpectationId);
        }
    }

    private static Guid? ResolveWorkflowRunIdForStep(IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
            if (lineage?.WorkflowRunId.HasValue == true)
            {
                return lineage.WorkflowRunId.Value;
            }

            var key = artifact.ExternalReferenceKey;
            if (!key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = "workflow-run:".Length;
            var length = Math.Min(36, key.Length - start);
            if (length <= 0)
            {
                continue;
            }

            if (Guid.TryParse(key.Substring(start, length), out var workflowRunId))
            {
                return workflowRunId;
            }
        }

        return null;
    }

    private static Guid? ResolveSubprocessRunIdForStep(IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
            if (lineage?.SubprocessRunId.HasValue == true)
            {
                return lineage.SubprocessRunId.Value;
            }

            var key = artifact.ExternalReferenceKey;
            if (!key.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = "subprocess-run:".Length;
            var length = Math.Min(36, key.Length - start);
            if (length <= 0)
            {
                continue;
            }

            if (Guid.TryParse(key.Substring(start, length), out var subprocessRunId))
            {
                return subprocessRunId;
            }
        }

        return null;
    }
}

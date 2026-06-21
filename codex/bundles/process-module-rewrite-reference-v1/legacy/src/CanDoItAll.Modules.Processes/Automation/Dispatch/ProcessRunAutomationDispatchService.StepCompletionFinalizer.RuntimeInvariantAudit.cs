using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<IReadOnlyList<RuntimeInvariantViolation>> PersistRuntimeInvariantAuditAsync(
        ProcessStepCompletionFinalizerContext context,
        ProcessStepRunStatus completionStatus,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuntimeInvariantViolation>();
        var candidate = context.Candidate;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == candidate.Run.Id && item.StepRunId == candidate.StepRun.Id)
            .ToListAsync(cancellationToken);

        if (context.ExecutionDetail is not null &&
            !ResolveProcessAllowsProductMutation(context.ExecutionDetail.Run) &&
            context.ExecutionDetail.ToolReceipts.Any(IsConcreteProductMutationReceipt))
        {
            violations.Add(new RuntimeInvariantViolation(
                ProcessConformanceSeverity.Critical,
                "product-mutation-without-operation",
                "A non-mutating governed step recorded a product mutation tool receipt.",
                "Tool receipts must match the persisted operation contract."));
        }

        foreach (var artifact in artifacts)
        {
            if (IsWrongRootArtifact(artifact))
            {
                violations.Add(new RuntimeInvariantViolation(
                    ProcessConformanceSeverity.High,
                    "wrong-root-artifact",
                    $"Artifact '{artifact.Title}' points at '{artifact.ManagedStoragePath}', which is outside the current-run managed artifact boundary.",
                    "Evidence and deliverables must be recorded from current-run managed storage or an explicitly allowed external artifact destination."));
            }

            if (RequiresProjectionLineage(artifact) &&
                string.IsNullOrWhiteSpace(artifact.ProjectionIdentityHash))
            {
                violations.Add(new RuntimeInvariantViolation(
                    ProcessConformanceSeverity.High,
                    "missing-projection-lineage",
                    $"Artifact '{artifact.Title}' is missing projection identity lineage.",
                    "Evidence and deliverable artifact records need typed source lineage for dedupe and recovery audit."));
            }
        }

        foreach (var unsatisfiedResult in validationResults.Where(result => !result.IsSatisfied))
        {
            violations.Add(new RuntimeInvariantViolation(
                ProcessConformanceSeverity.Moderate,
                "artifact-validation-unsatisfied",
                $"Artifact expectation '{unsatisfiedResult.ExpectationTitle}' was not satisfied: {unsatisfiedResult.Diagnostic}",
                unsatisfiedResult.SuggestedAction));
        }

        if (violations.Count == 0)
        {
            return [];
        }

        foreach (var violation in violations)
        {
            await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                new ProcessConformanceObservation
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    Severity = violation.Severity,
                    Category = "runtime-invariant",
                    Observation = violation.Observation,
                    DeviationReason = violation.DeviationReason,
                    IsSafeNonAction = false,
                    ContainsSensitiveAssessment = false,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    EventType = ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded,
                    Title = "Runtime invariant violation recorded",
                    Description = violation.Observation,
                    CorrelationId = $"{candidate.StepRun.Id:D}:{violation.Code}",
                    OperatingMode = candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(new
                    {
                        RunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        CompletionStatus = completionStatus.ToString(),
                        violation.Code,
                        Severity = violation.Severity.ToString(),
                        violation.DeviationReason
                    }),
                    OccurredAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return violations;
    }

    private static bool IsConcreteProductMutationReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        if (!ConcreteProductMutationToolNames.Contains(receipt.ToolName))
        {
            return false;
        }

        var summary = CollapsePromptWhitespace(string.Join(' ', receipt.RequestSummary, receipt.WorkingDirectory));
        if (summary.Contains("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return !summary.Contains("/artifact", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/artifacts", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/evidence", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/output", StringComparison.OrdinalIgnoreCase);
        }

        return summary.Contains("/src/", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("\\src\\", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains(" output/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWrongRootArtifact(ProcessArtifactRecord artifact)
    {
        return !ProcessArtifactLineageValidator
            .ValidateManagedStorageBoundary(artifact, artifact.ProcessRunId)
            .IsCurrentRun;
    }

    private static bool RequiresProjectionLineage(ProcessArtifactRecord artifact)
    {
        return artifact.ArtifactKind is ProcessArtifactKind.Evidence or ProcessArtifactKind.Deliverable;
    }
}

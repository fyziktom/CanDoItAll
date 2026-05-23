using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.Logging;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<DispatchExecutionOutcome> TryRecoverMissingCompletionArtifactsAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        if (executionOutcome.CompletionStatus != ProcessStepRunStatus.Completed)
        {
            return executionOutcome;
        }

        var missingArtifacts = ResolveMissingRequiredCompletionArtifacts(candidate);
        if (missingArtifacts.Count == 0)
        {
            return executionOutcome;
        }

        var missingArtifactSummary = FormatMissingArtifactTitles(missingArtifacts);
        var recoveryReason = $"Required artifacts remained missing after completed execution run {executionOutcome.Detail.Run.Id:D}: {missingArtifactSummary}.";
        var recoveryDecision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.ArtifactMissing,
            recoveryReason,
            executionOutcome.AttemptNumber,
            executionOutcome.Detail.Run.Id.ToString("D"),
            nextAttemptAtUtc: clock.GetUtcNow());
        var recoveryPacket = CreateReworkPacketForDecision(
            candidate,
            executionOutcome.Detail,
            recoveryDecision,
            [],
            [],
            clock.GetUtcNow());
        if (recoveryPacket is not null)
        {
            recoveryDecision = recoveryDecision with
            {
                Mode = AgentRecoveryMode.ReworkContinuation,
                ReworkPacketId = recoveryPacket.Id
            };
        }

        await PersistRecoveryJournalAsync(
            candidate,
            recoveryDecision,
            recoveryPacket,
            providerFallbackCount: 0,
            cancellationToken);

        logger.LogWarning(
            "Completed AgentFramework execution run {ExecutionRunId} for process run {RunId}, step {StepRunId} left required artifacts missing: {MissingArtifacts}. Starting targeted artifact recovery before step transition.",
            executionOutcome.Detail.Run.Id,
            candidate.Run.Id,
            candidate.StepRun.Id,
            missingArtifactSummary);

        var recoveryDirective = BuildTypedRecoveryDirective(
            recoveryDecision,
            recoveryPacket,
            BuildCompletionArtifactRecoveryDirective(candidate, executionOutcome, missingArtifacts));
        var recoveryCandidate = candidate with
        {
            ChatSessionId = null,
            RecoveryExecutionRunId = null,
            ManualRecoveryDirective = recoveryDirective
        };
        var recoveryOutcome = await ExecuteUntilSettledAsync(
            recoveryCandidate,
            $"{NormalizeTrigger(trigger, candidate.StepRun.Id)}:completion-artifact-recovery",
            renewLeaseAsync,
            cancellationToken);

        await ProjectExecutionArtifactsAsync(
            recoveryCandidate,
            recoveryOutcome.Detail,
            recoveryOutcome.ResponseText,
            recoveryOutcome.CompletionStatus,
            cancellationToken);

        var remainingArtifacts = ResolveMissingRequiredCompletionArtifacts(candidate);
        if (remainingArtifacts.Count == 0)
        {
            return recoveryOutcome with
            {
                CompletionStatus = ProcessStepRunStatus.Completed,
                CompletionReason = $"{recoveryOutcome.CompletionReason} Targeted artifact recovery recorded the previously missing required artifact(s): {missingArtifactSummary}."
            };
        }

        var remainingArtifactSummary = FormatMissingArtifactTitles(remainingArtifacts);
        logger.LogWarning(
            "Targeted artifact recovery for process run {RunId}, step {StepRunId} did not record all required artifacts. Remaining artifacts: {RemainingArtifacts}.",
            candidate.Run.Id,
            candidate.StepRun.Id,
            remainingArtifactSummary);

        return recoveryOutcome with
        {
            CompletionStatus = ProcessStepRunStatus.Blocked,
            CompletionReason = $"Required artifacts remain missing after targeted artifact recovery: {remainingArtifactSummary}. The step is blocked instead of retrying the full implementation loop again.",
            SelectedBranchOutcomeId = null
        };
    }

    private static IReadOnlyList<DispatchArtifactExpectation> ResolveMissingRequiredCompletionArtifacts(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts
            .Where(artifact => artifact.IsRequired)
            .Where(artifact => !candidate.RecordedArtifactExpectationIds.Contains(artifact.Id))
            .ToList();
    }

    private static string BuildCompletionArtifactRecoveryDirective(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        IReadOnlyList<DispatchArtifactExpectation> missingArtifacts)
    {
        var builder = new StringBuilder();
        var missingArtifactSummary = FormatMissingArtifactTitles(missingArtifacts);
        builder.AppendLine("Targeted completion-artifact recovery is required.");
        builder.AppendLine($"The previous AgentFramework execution run completed the step but the process still lacks required artifact record(s): {missingArtifactSummary}.");
        builder.AppendLine($"Previous execution run id: {executionOutcome.Detail.Run.Id:D}.");
        builder.AppendLine("Use current-run records, upstream artifacts, tool receipts, changed files, build/test/run logs, and project-structure context to create only the missing handoff artifact(s).");
        builder.AppendLine("Do not repeat broad implementation work, regenerate unrelated product files, or restart from the original request when the existing current-run evidence is enough.");
        builder.AppendLine("If product files or validation evidence are insufficient to produce an honest artifact, return Blocked with the exact missing record, source file, tool receipt, or upstream decision instead of inventing evidence.");
        builder.AppendLine("If the artifact can be produced, write it as a durable current-run artifact with workspace_write_file and then return a valid ProcessStepOutcomeResult with Status Completed.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(missingArtifacts);
        if (governedInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Expected artifact path(s) to stat after writing: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
        }

        if (governedInspectionPaths.ReadPaths.Count > 0)
        {
            builder.AppendLine($"Expected text artifact path(s) to read back after writing: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
        }
        else
        {
            builder.AppendLine($"Use the current-run managed artifact root for new evidence files: {BuildCurrentRunManagedArtifactRoot(candidate)}.");
        }

        if (candidate.ArtifactInputs.Count > 0)
        {
            builder.AppendLine("Inspect upstream artifacts before writing recovery artifacts when they contain the source contract or prior-step outputs.");
        }

        foreach (var input in candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Take(8))
        {
            if (!string.IsNullOrWhiteSpace(input.ManagedStoragePath))
            {
                builder.AppendLine($"Upstream artifact to inspect: {input.Title} at {input.ManagedStoragePath}.");
            }
        }

        builder.AppendLine("The recovery output must satisfy the named artifact expectation titles exactly so the process can transition without another full-step retry.");
        return builder.ToString().Trim();
    }

    private static string FormatMissingArtifactTitles(IReadOnlyList<DispatchArtifactExpectation> artifacts)
    {
        return string.Join(", ", artifacts.Select(artifact => artifact.Title));
    }
}

using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static DispatchExecutionOutcome? TryCreateProviderFailureArtifactRecoveryOutcome(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        ProcessExecutionPostAttemptFacts postAttemptFacts,
        int attemptNumber)
    {
        var recoveryReason = string.Empty;
        if (!ShouldRecoverFailedFinalizerRepairFromRequiredArtifacts(
                candidate,
                detail,
                responseText,
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures,
                postAttemptFacts.CarriedImplementationProof,
                out recoveryReason) &&
            !ShouldRecoverFailedAgentRuntimeProviderFailureFromRequiredArtifacts(
                candidate,
                detail,
                responseText,
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures,
                postAttemptFacts.CarriedImplementationProof,
                out recoveryReason))
        {
            return null;
        }

        return new DispatchExecutionOutcome(
            detail,
            responseText,
            ProcessStepRunStatus.Completed,
            recoveryReason,
            [],
            attemptNumber,
            postAttemptFacts.SelectedBranchOutcomeId);
    }

    private static bool ShouldRecoverFailedFinalizerRepairFromRequiredArtifacts(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        CarriedImplementationProof carriedImplementationProof,
        out string recoveryReason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(unresolvedCriticalToolFailures);

        recoveryReason = string.Empty;
        if (!TryResolveRequiredFinalizerRepairProviderFailure(detail, responseText, out var finalizerRepairFailureSummary))
        {
            return false;
        }

        return ShouldRecoverFailedProviderFailureFromRequiredArtifacts(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            carriedImplementationProof,
            "failed during required finalizer repair after the initial response omitted the governed finalizer",
            "Finalizer repair failure",
            finalizerRepairFailureSummary,
            out recoveryReason);
    }

    private static bool ShouldRecoverFailedAgentRuntimeProviderFailureFromRequiredArtifacts(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        CarriedImplementationProof carriedImplementationProof,
        out string recoveryReason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(unresolvedCriticalToolFailures);

        recoveryReason = string.Empty;
        if (!TryResolveAgentRuntimeProviderFailureAfterArtifactProgress(detail, responseText, out var providerFailureSummary))
        {
            return false;
        }

        return ShouldRecoverFailedProviderFailureFromRequiredArtifacts(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            carriedImplementationProof,
            "failed after provider activity before the governed finalizer could be persisted",
            "Provider failure",
            providerFailureSummary,
            out recoveryReason);
    }

    private static bool ShouldRecoverFailedProviderFailureFromRequiredArtifacts(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        CarriedImplementationProof carriedImplementationProof,
        string failureContext,
        string failureLabel,
        string failureSummary,
        out string recoveryReason)
    {
        recoveryReason = string.Empty;
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            !candidate.ExpectedArtifacts.Any(artifact => artifact.IsRequired) ||
            missingRequiredTools.Count > 0 ||
            unresolvedCriticalToolFailures.Count > 0 ||
            detail.Run.PendingApprovals.Count > 0 ||
            !AllRequiredArtifactsHaveDurableCurrentRunEvidence(candidate, detail))
        {
            return false;
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var blockerSummary = ProcessCompletionBlockerRules.CreateSummary(
            ResolveMissingUpstreamArtifactInputSummary(candidate),
            ResolveMissingConcreteProofSummary(candidate, inspectionText),
            ResolveIncompleteImplementationSummary(candidate, inspectionText),
            ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
                candidate,
                detail,
                carriedImplementationProof),
            ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
                candidate,
                detail,
                carriedImplementationProof),
            ResolveInvalidBrowserProofSummary(candidate, detail),
            ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText),
            ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText),
            ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText),
            ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail),
            ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText),
            ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText));
        if (blockerSummary.HasAny)
        {
            return false;
        }

        recoveryReason = $"AgentFramework execution run {detail.Run.Id:D} produced all required durable current-run artifacts, but {failureContext}. Process-owned recovery will project the artifacts and finalize the step without rerunning the executor. {failureLabel}: {failureSummary}";
        return true;
    }

    private static bool AllRequiredArtifactsHaveDurableCurrentRunEvidence(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return candidate.ExpectedArtifacts
            .Where(artifact => artifact.IsRequired)
            .All(artifact =>
                HasRecordedExpectedArtifact(candidate, detail, artifact) ||
                CanProjectWorkspaceWrittenArtifact(candidate, detail, artifact) ||
                CanProjectProviderNativeVisualArtifact(candidate, detail, artifact));
    }

    private static bool TryResolveRequiredFinalizerRepairProviderFailure(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (detail.Run.State != ProcessAutomationExecutionState.Failed ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Failed ||
            !WasRequiredFinalizerRepairRequested(detail))
        {
            return false;
        }

        var candidateTexts = BuildFinalizerRepairFailureTexts(detail, responseText);
        if (!candidateTexts.Any(MentionsBoundedRequiredFinalizerRepairTurnFailure) &&
            !TryResolveFinalizerRecoveryUsageProviderFailure(detail, out failureSummary))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(failureSummary) &&
            !TryResolveFinalizerRepairTextProviderFailure(candidateTexts, out failureSummary))
        {
            failureSummary = "The assigned provider failed during the bounded required-finalizer repair turn after the initial response completed without the required finalizer.";
        }

        return true;
    }

    private static bool TryResolveAgentRuntimeProviderFailureAfterArtifactProgress(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (detail.Run.State != ProcessAutomationExecutionState.Failed ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Failed ||
            !detail.ToolReceipts.Any(receipt => !IsFailedToolReceipt(receipt)))
        {
            return false;
        }

        var candidateTexts = BuildProviderFailureTexts(detail, responseText);
        if (!candidateTexts.Any(MentionsProviderRuntimeFailedAfterProviderActivity) &&
            !TryResolveAgentRuntimeUsageProviderFailure(detail, out failureSummary))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(failureSummary) &&
            !TryResolveProviderFailureText(candidateTexts, out failureSummary))
        {
            failureSummary = "The assigned provider failed after successful tool activity before returning a governed finalizer.";
        }

        return true;
    }

    private static bool WasRequiredFinalizerRepairRequested(ProcessAutomationExecutionRunDetail detail)
    {
        return detail.ExecutionLog.Any(entry =>
            string.Equals(entry.Phase, "Finalizer repair", StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains(AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string?> BuildFinalizerRepairFailureTexts(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        return
        [
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
            detail.Run.ResultSummary,
            .. detail.ExecutionLog.Select(entry => entry.Message),
            .. detail.UsageObservations.Select(observation => observation.DiagnosticsJson)
        ];
    }

    private static IReadOnlyList<string?> BuildProviderFailureTexts(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        return
        [
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
            detail.Run.ResultSummary,
            .. detail.ExecutionLog.Select(entry => entry.Message),
            .. detail.UsageObservations.Select(observation => observation.DiagnosticsJson)
        ];
    }

    private static bool TryResolveFinalizerRecoveryUsageProviderFailure(
        ProcessAutomationExecutionRunDetail detail,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        foreach (var usageObservation in detail.UsageObservations)
        {
            if (!string.Equals(usageObservation.SourcePhase, ProviderUsageSourcePhases.FinalizerRecovery, StringComparison.OrdinalIgnoreCase) ||
                !ProcessRecoverableProviderFailureRules.TryMapSummary(usageObservation.DiagnosticsJson, out failureSummary))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryResolveAgentRuntimeUsageProviderFailure(
        ProcessAutomationExecutionRunDetail detail,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        foreach (var usageObservation in detail.UsageObservations)
        {
            if (!string.Equals(usageObservation.SourcePhase, ProviderUsageSourcePhases.AgentRuntime, StringComparison.OrdinalIgnoreCase) ||
                !ProcessRecoverableProviderFailureRules.TryMapSummary(usageObservation.DiagnosticsJson, out failureSummary))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryResolveFinalizerRepairTextProviderFailure(
        IReadOnlyList<string?> candidateTexts,
        out string failureSummary)
        => TryResolveProviderFailureText(candidateTexts, out failureSummary);

    private static bool TryResolveProviderFailureText(
        IReadOnlyList<string?> candidateTexts,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        foreach (var candidateText in candidateTexts)
        {
            if (ProcessRecoverableProviderFailureRules.TryMapSummary(candidateText, out failureSummary))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MentionsBoundedRequiredFinalizerRepairTurnFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("bounded required-finalizer repair turn", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("provider", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsProviderRuntimeFailedAfterProviderActivity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Provider runtime failed after provider activity", StringComparison.OrdinalIgnoreCase);
    }
}

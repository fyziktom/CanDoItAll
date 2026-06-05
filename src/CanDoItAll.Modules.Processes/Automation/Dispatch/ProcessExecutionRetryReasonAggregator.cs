using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessExecutionRetryReasonAggregator
    {
        public static IReadOnlyList<string> ResolveIncompleteSuccessfulRunRetryReasons(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            CarriedImplementationProof carriedImplementationProof)
        {
            var reasons = new List<string>();
            if (missingRequiredTools.Count > 0)
            {
                reasons.Add($"missing required tools: {string.Join(", ", missingRequiredTools)}");
            }

            var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
            if (unresolvedCriticalToolFailures.Count > 0)
            {
                reasons.Add(
                    "unresolved critical tool failures: " +
                    string.Join(
                        "; ",
                        unresolvedCriticalToolFailures
                            .Take(2)
                            .Select(item => $"{item.ToolName}: {item.ExitSummary}")));
            }

            if (IsRecoverableImplementationPunt(candidate, responseText))
            {
                reasons.Add("recoverable implementation punt");
            }

            var inspectionText = ResolveOutputInspectionText(responseText);
            AddRetryReason(reasons, "incomplete implementation", ResolveIncompleteImplementationSummary(candidate, inspectionText));
            AddRetryReason(reasons, "missing concrete proof", ResolveMissingConcreteProofSummary(candidate, inspectionText));
            AddRetryReason(
                reasons,
                "missing concrete implementation proof",
                ResolveMissingConcreteImplementationProofSummaryWithCarryForward(candidate, detail, carriedImplementationProof));
            AddRetryReason(reasons, "missing runnable application proof", ResolveMissingRunnableApplicationProofSummary(candidate, detail));
            AddRetryReason(reasons, "invalid browser proof", ResolveInvalidBrowserProofSummary(candidate, detail));
            AddRetryReason(reasons, "invalid quality validation proof", ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText));
            AddRetryReason(reasons, "missing required artifact", ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText));
            AddRetryReason(reasons, "downgraded project-structure requirement", ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText));
            AddRetryReason(reasons, "missing upstream artifact inspection", ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail));
            AddRetryReason(reasons, "stale or ungrounded product path reference", ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText));
            AddRetryReason(reasons, "shared managed artifact collision risk", ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText));

            if (IsRecoverableGovernedOutcomeGap(candidate, responseText) &&
                !CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, inspectionText))
            {
                reasons.Add("recoverable governed outcome gap");
            }

            if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
            {
                reasons.Add($"recoverable provider failure: {providerFailureSummary}");
            }

            if (TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out var finalizerFailureSummary))
            {
                reasons.Add($"recoverable finalizer validation failure: {finalizerFailureSummary}");
            }

            if (TryResolveRecoverableExecutionInterruption(detail, responseText, out var interruptionSummary))
            {
                reasons.Add($"recoverable execution interruption: {interruptionSummary}");
            }

            if (detail.Run.State == ProcessAutomationExecutionState.Failed &&
                (MentionsRepeatedToolInvocation(responseText) || MentionsRepeatedToolInvocation(detail.Run.ResultSummary)))
            {
                reasons.Add("recoverable repeated tool invocation");
            }

            return reasons;
        }

        public static bool IsNoProgressRetryReason(string retryReason)
        {
            return retryReason.StartsWith("missing required tools:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("unresolved critical tool failures:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("missing concrete proof:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("missing concrete implementation proof:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("missing runnable application proof:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("invalid browser proof:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("invalid quality validation proof:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("missing required artifact:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("missing upstream artifact inspection:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("stale or ungrounded product path reference:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("shared managed artifact collision risk:", StringComparison.OrdinalIgnoreCase) ||
                   retryReason.StartsWith("recoverable finalizer validation failure:", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddRetryReason(List<string> reasons, string label, string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            reasons.Add($"{label}: {summary}");
        }
    }
}

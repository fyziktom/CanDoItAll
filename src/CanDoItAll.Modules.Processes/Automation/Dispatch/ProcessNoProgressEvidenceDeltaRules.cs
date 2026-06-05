using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessNoProgressEvidenceDeltaRules
    {
        public static bool HasNewSatisfiedCurrentAttemptEvidence(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail)
        {
            return detail.Artifacts.Any(artifact =>
                       candidate.ExpectedArtifacts.Any(expectation =>
                           !candidate.RecordedArtifactExpectationIds.Contains(expectation.Id) &&
                           ResolveArtifactExpectation(candidate, artifact)?.Id == expectation.Id)) ||
                   detail.ToolReceipts.Any(receipt =>
                       !IsFailedToolReceipt(receipt) &&
                       ImplementationProofToolNames.Contains(NormalizeToolToken(receipt.ToolName), StringComparer.Ordinal));
        }
    }
}

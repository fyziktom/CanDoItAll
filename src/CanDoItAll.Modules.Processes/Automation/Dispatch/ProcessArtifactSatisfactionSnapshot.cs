using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactSatisfactionSnapshot(
    IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    IReadOnlySet<Guid> RecordedArtifactExpectationIds,
    IReadOnlyList<ProcessAutomationExecutionArtifact> ExecutionArtifacts,
    IReadOnlyList<ProcessAutomationToolExecutionReceipt> ToolReceipts);

internal static class ProcessArtifactSatisfactionSnapshotBuilder
{
    public static ProcessArtifactSatisfactionSnapshot From(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return new ProcessArtifactSatisfactionSnapshot(
            candidate.ExpectedArtifacts
                .Select(ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation)
                .ToList(),
            candidate.RecordedArtifactExpectationIds,
            detail.Artifacts,
            detail.ToolReceipts);
    }
}

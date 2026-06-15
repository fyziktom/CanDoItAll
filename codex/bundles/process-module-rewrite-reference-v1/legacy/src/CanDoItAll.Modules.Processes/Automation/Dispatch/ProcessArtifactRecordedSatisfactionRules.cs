using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactRecordedSatisfactionRules
{
    public static bool HasRecordedExpectedArtifact(
        ProcessArtifactSatisfactionSnapshot snapshot,
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact,
        Func<ProcessAutomationExecutionArtifact, Guid?> resolveArtifactExpectationId)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordedSatisfactionRules.HasRecordedExpectedArtifact(
            expectedArtifact.Id,
            snapshot.RecordedArtifactExpectationIds,
            snapshot.ExecutionArtifacts.Select(resolveArtifactExpectationId));
    }
}


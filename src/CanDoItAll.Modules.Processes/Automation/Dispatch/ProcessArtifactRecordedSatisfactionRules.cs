using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactRecordedSatisfactionRules
{
    public static bool HasRecordedExpectedArtifact(
        ProcessArtifactSatisfactionSnapshot snapshot,
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact,
        Func<ProcessAutomationExecutionArtifact, Guid?> resolveArtifactExpectationId)
    {
        return snapshot.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
               snapshot.ExecutionArtifacts.Any(artifact => resolveArtifactExpectationId(artifact) == expectedArtifact.Id);
    }
}


using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ToolOwnerObservationTests {
    [Theory]
    [InlineData(WorkflowLaunchObservation.Confirmed, false)]
    [InlineData(WorkflowLaunchObservation.RecoveredAfterObserverFailure, false)]
    [InlineData(WorkflowLaunchObservation.AdmissionReceiptPending, true)]
    public void Workflow_owner_observation_distinguishes_recovered_acknowledgement_from_pending_receipt(
        WorkflowLaunchObservation observation, bool requiresReconciliation) {
        var now = DateTimeOffset.UtcNow;
        var result = new WorkflowAgentStartResult(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "saved run", now, now, null),
            WorkflowAgentDefinitionSelectionMode.ExactSavedVersion, WorkflowRuntimeBackendKind.InProcess,
            WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, "owner observation") { Observation = observation };
        Assert.Equal(requiresReconciliation, ((IAgentToolOwnerObservationEvidence)result).RequiresOwnerReconciliation);
        var json = JsonSerializer.SerializeToElement(result);
        Assert.False(json.TryGetProperty(nameof(IAgentToolOwnerObservationEvidence.RequiresOwnerReconciliation), out _));
        var restored = json.Deserialize<WorkflowAgentStartResult>()!;
        Assert.Equal(result.Run.RunId, restored.Run.RunId);
        Assert.Equal(observation, restored.Observation);
        Assert.Equal(requiresReconciliation, restored.RequiresOwnerReconciliation);
    }

    [Theory]
    [InlineData(ProcessLaunchContinuationState.Prepared, false)]
    [InlineData(ProcessLaunchContinuationState.Accepted, false)]
    [InlineData(ProcessLaunchContinuationState.Continuing, false)]
    [InlineData(ProcessLaunchContinuationState.Started, false)]
    [InlineData(ProcessLaunchContinuationState.ReconciliationRequired, true)]
    [InlineData(ProcessLaunchContinuationState.Failed, false)]
    [InlineData(null, false)]
    public void Process_owner_observation_retains_the_admitted_identity_without_serializing_a_new_wire_flag(
        ProcessLaunchContinuationState? state, bool requiresReconciliation) {
        var runId = Guid.NewGuid();
        var result = new ProjectStructureProcessNodeStartResult(Guid.NewGuid(), "selected-node", Guid.NewGuid(),
            Guid.NewGuid(), runId, "accepted", "/processes", null, []) {
            Observation = state is { } continuation
                ? new(new(Guid.NewGuid()), new(runId), continuation, ProcessLaunchLinkDeliveryState.Pending, "owner observation")
                : null
        };
        Assert.Equal(requiresReconciliation, ((IAgentToolOwnerObservationEvidence)result).RequiresOwnerReconciliation);
        var json = JsonSerializer.SerializeToElement(result);
        Assert.False(json.TryGetProperty(nameof(IAgentToolOwnerObservationEvidence.RequiresOwnerReconciliation), out _));
        var restored = json.Deserialize<ProjectStructureProcessNodeStartResult>()!;
        Assert.Equal(runId, restored.RunId);
        Assert.Equal(result.Observation?.AdmissionId, restored.Observation?.AdmissionId);
        Assert.Equal(state, restored.Observation?.ContinuationState);
        Assert.Equal(requiresReconciliation, restored.RequiresOwnerReconciliation);
    }
}

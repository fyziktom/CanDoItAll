using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Support;

public static class ProcessProjectAdmissionFixture {
    public static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    public static ProcessRuntimeCommitRequest Initial(ProcessProjectAdmission? admission = null) {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var definitionId = ProcessStepDefinitionId.New();
        var binding = new ProcessStrategyBindingSnapshot(new DriverId("driver.admission-test"),
            new StrategyId("strategy.admission-test.execute"), "1.0.0", "factory.1.0.0",
            "runtime/1.0", "runtime/1.0", "sha256:binding", []);
        var plan = new ProcessInstancePlan(
            new(planId, planId, null, null, "processes.instance-plan.v1", Now, 0),
            new(ProcessDefinitionId.New(), ProcessDefinitionVersionId.New(), "sha256:definition", "runtime/1.0", "runtime/1.0", [], [], []),
            new DriverStackSnapshot([new(binding.DriverId, "1.0.0", ProcessDriverLayer.Scenario,
                "runtime/1.0", "runtime/1.0", new HashSet<CapabilityTag>())]) {
                HostProfileId = binding.HostProfileId, HostCapabilities = binding.HostCapabilities
            },
            new([binding], [], [], []),
            [new(stepId, definitionId, "execute", ProcessStepKind.Activity, true, false, binding)],
            new([], []), new([]), [], new("sha256:manager", null, [], []), new([]),
            new(false, "sha256:projection"), new("sha256:governance", []), string.Empty);
        plan = plan with { PlanHash = ProcessPlanHasher.Compute(plan) };
        var state = new ProcessRuntimeStateSnapshot(runId, runId, planId, plan.PlanHash, ProcessRuntimeStatus.Created,
            [new(stepId, definitionId, ProcessRuntimeStepStatus.Planned, true, 0, new HashSet<ProcessStepInstanceId>(),
                new HashSet<ArtifactSlotId>(), null, null)],
            [], [], new HashSet<ArtifactSlotId>(), Now) { ProjectAdmission = admission };
        var runtimeEvent = Event(state, ProcessRuntimeEventTypes.ProcessRunCreated);
        var mutation = new ProcessRuntimeMutation(ProcessRuntimeTransitionOutcome.Applied, state, [runtimeEvent],
            [new(RuntimeOutboxMessageId.New(), runtimeEvent.EventId, ProcessOutboxSubscriberKind.RuntimeProjection, runtimeEvent.PayloadHash)],
            [new(ArtifactLedgerEventId.New(), runtimeEvent.EventId, ArtifactSlotId.New(), ArtifactInstanceId.New(), "sha256:artifact")], []);
        return new(RuntimeCommandId.New(), state, mutation, InitialPlan: plan) {
            InitialAssignments = [new(runId, planId, stepId, "execute", "role", "role", "Test role",
                ProcessLaunchExecutorKinds.Agent, Guid.NewGuid().ToString("D"), "Fixture executor", "Execute.",
                "sha256:readiness", "Fixture binding", [], [], [ProcessOperationContractNames.ReadProjectStructure],
                ProcessOperationContractNames.ExternalProductTargetMutable, new Dictionary<string, string>(), null, Now)]
        };
    }

    public static ProcessRuntimeCommitRequest Cancel(ProcessRuntimeStateSnapshot original) {
        var state = original with { Status = ProcessRuntimeStatus.Cancelled, UpdatedAtUtc = original.UpdatedAtUtc.AddSeconds(1) };
        var runtimeEvent = Event(state, ProcessRuntimeEventTypes.ProcessRunCancelled);
        return new(RuntimeCommandId.New(), original, new(ProcessRuntimeTransitionOutcome.Applied, state, [runtimeEvent],
            [new(RuntimeOutboxMessageId.New(), runtimeEvent.EventId, ProcessOutboxSubscriberKind.RuntimeProjection, runtimeEvent.PayloadHash)], [], []));
    }

    public static ProcessRuntimeCommitRequest WithAdmission(ProcessRuntimeCommitRequest request, ProcessProjectAdmission? admission)
        => request with {
            OriginalState = request.OriginalState with { ProjectAdmission = admission },
            Mutation = request.Mutation with { State = request.Mutation.State with { ProjectAdmission = admission } }
        };

    private static ProcessRuntimeEventEnvelope Event(ProcessRuntimeStateSnapshot state, ProcessEventType eventType)
        => new(RuntimeEventId.New(), state.RootRunId, state.RunId, new ProcessCorrelationId("admission-test"), null,
            new(ProcessEventActorKind.System, new ProcessActorId("admission-test")),
            ProcessContractVersions.RuntimeEventEnvelopeV1, ProcessEventSensitivity.Normal, state.UpdatedAtUtc, eventType, "sha256:event");
}

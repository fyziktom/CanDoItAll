using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessExecutionResultFactoryTests {
    [Fact]
    public void Host_capability_failure_retains_observed_evidence_and_predispatch_retry_safety() {
        var assignment = Assert.Single(ProcessPreparedLaunchFixture.Create().InitialCommit.InitialAssignments!);
        var evidence = new ProcessHostCapabilityEvaluationEvidence(new("windows-headless"), []);
        var preflight = new ProcessRuntimeToolPreflightResult(false, [ToolContractCatalog.WorkspaceReadFile], "Unavailable") {
            HostCapabilityEvidence = evidence
        };

        var result = ProcessExecutionResultFactory.CreateHostCapabilityFailureResult(assignment, preflight);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Same(evidence, result.HostCapabilityEvidence);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.runtime_tool_preflight_failed", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Null(result.ExecutionRunId);
    }

    [Fact]
    public void Changed_tool_contract_requires_repair_and_preserves_step_bound_evidence() {
        var assignment = Assert.Single(ProcessPreparedLaunchFixture.Create().InitialCommit.InitialAssignments!);

        var result = ProcessExecutionResultFactory.CreateRuntimeToolContractChangedResult(assignment);
        var otherStep = ProcessExecutionResultFactory.CreateRuntimeToolContractChangedResult(
            assignment with { StepInstanceId = ProcessStepInstanceId.New() });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.runtime_tool_contract_changed", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Unknown, diagnostic.Idempotency);
        Assert.NotEqual(result.ResultHash, otherStep.ResultHash);
        Assert.NotEqual(diagnostic.EvidenceHash, Assert.Single(otherStep.Diagnostics).EvidenceHash);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Attaching_host_evidence_preserves_the_complete_execution_result() {
        var result = ProcessExecutionAdapterResult.Succeeded("Completed", "sha256:retained") with {
            ExecutionRunId = new(Guid.NewGuid())
        };
        var evidence = new ProcessHostCapabilityEvaluationEvidence(new("windows-headless"), []);

        var observed = ProcessExecutionResultFactory.AttachHostCapabilityEvidence(result, evidence);

        Assert.Same(result, ProcessExecutionResultFactory.AttachHostCapabilityEvidence(result, null));
        Assert.Same(evidence, observed.HostCapabilityEvidence);
        Assert.Equal(result, observed with { HostCapabilityEvidence = null });
    }
}

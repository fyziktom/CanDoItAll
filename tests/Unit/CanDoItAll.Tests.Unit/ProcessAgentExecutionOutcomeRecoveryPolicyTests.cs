using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Direct unit coverage for <see cref="ProcessAgentExecutionOutcomeRecoveryPolicy"/> (SB13): the success path and
/// every rejection/inapplicability condition ported from the deleted MAF <c>ProcessArtifactRecoveryService</c>
/// coverage (previously in AgentFinalizerPolicyTests.cs / MafRuntimeArchitectureServicesTests.cs), now instantiating
/// the extracted owner directly and driving it exclusively through <see cref="IAgentExecutionOutcomeRecoveryPolicy.Evaluate"/>
/// &#8212; the same public contract the MAF coordinator calls &#8212; so a regression that merely delegates back to
/// the deleted monolith (or trusts the wrong authority) has nowhere to hide.
/// </summary>
public sealed class ProcessAgentExecutionOutcomeRecoveryPolicyTests
{
    [Fact]
    public void Evaluate_recovers_completed_outcome_from_primary_artifact()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");
        var artifactRef = BuildArtifactRef(runId, "code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, """
                # Feature implementation change set

                Status: Completed

                ## Changed files
                - external-target/C/programovani/dotnet/output/src/App/App.csproj
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Recovered, decision.Status);
        Assert.Equal(artifactRef, decision.EvidenceReference);
        Assert.Equal("Completed", decision.OutcomeStatusLabel);
        Assert.Contains("required finalizer", decision.RecoveryReason, StringComparison.OrdinalIgnoreCase);
        var outcome = DeserializeOutcome(decision);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, outcome.Status);
        Assert.Equal([artifactRef], outcome.EvidenceRefs);
        Assert.Empty(outcome.NextActions);
        Assert.Contains("Status: Completed", outcome.HumanReadableSummaryMarkdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_reason_reflects_provider_streaming_timeout_cause()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "targeted-validation");
        var artifactRef = BuildArtifactRef(runId, "targeted-validation");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, "Status: Completed"));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Recovered, decision.Status);
        Assert.Contains("provider streaming timed out", decision.RecoveryReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_never_projects_branch_outcome_key_from_artifact_text()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "targeted-validation");
        var artifactRef = BuildArtifactRef(runId, "targeted-validation");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, """
                # Targeted validation

                Status: Completed
                Branch outcome key: feature-accepted

                ## Evidence
                - workspace_dotnet_restore exit code 0
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Recovered, decision.Status);
        var outcome = DeserializeOutcome(decision);
        Assert.Empty(outcome.BranchOutcomeKey);
    }

    [Fact]
    public void Evaluate_recovers_blocked_artifact_with_concrete_evidence()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "qa-validation");
        var artifactRef = BuildArtifactRef(runId, "qa-validation");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, $$"""
                # QA validation

                Status: Blocked

                Cannot proceed because workspace_dotnet_test failed with exit code 1.
                Evidence: {{artifactRef}}
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Recovered, decision.Status);
        var outcome = DeserializeOutcome(decision);
        Assert.Equal(ProcessStepOutcomeStatus.Blocked, outcome.Status);
        Assert.Equal([artifactRef], outcome.EvidenceRefs);
        Assert.NotEmpty(outcome.NextActions);
    }

    [Fact]
    public void Evaluate_is_not_applicable_for_a_different_output_type()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");
        var artifactRef = BuildArtifactRef(runId, "code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, "Status: Completed"),
            outputType: typeof(CodeReviewResult));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.NotApplicable, decision.Status);
        Assert.Empty(decision.MachineOutputJson);
    }

    [Theory]
    [InlineData(false, "process-step", "run", "step", "IsGovernedProcessStep")]
    [InlineData(true, "manual", "run", "step", "SourceKind")]
    [InlineData(true, "process-step", "", "step", "ProcessRunId")]
    [InlineData(true, "process-step", "run", "", "SourceId")]
    public void Evaluate_is_not_applicable_for_a_non_governed_context(
        bool isGovernedProcessStep,
        string sourceKind,
        string processRunId,
        string sourceId,
        string _)
    {
        var context = new AgentRuntimeContextIntent(
            SourceKind: sourceKind,
            SourceId: sourceId,
            ProcessRunId: processRunId,
            ProcessStepId: Guid.NewGuid().ToString("D"),
            TargetScope: "ExternalProductTargetMutable",
            IsGovernedProcessStep: isGovernedProcessStep,
            BrowserToolsAllowed: false,
            AllowsProductMutation: true,
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            AllowedOperations: ["MutateProductTarget", "WriteManagedProcessArtifacts"]);
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [],
            new FixedTextArtifactReader("unused", "Status: Completed"));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.NotApplicable, decision.Status);
    }

    [Fact]
    public void Evaluate_rejects_a_non_guid_process_run_id()
    {
        var context = CreateGovernedProcessContext(Guid.NewGuid(), "code-change") with
        {
            ProcessRunId = "not-a-guid"
        };
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [],
            new FixedTextArtifactReader("unused", "Status: Completed"));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("GUID", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_rejects_an_unsafe_step_artifact_file_name()
    {
        var context = CreateGovernedProcessContext(Guid.NewGuid(), "../code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [],
            new FixedTextArtifactReader("unused", "Status: Completed"));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("safe artifact file name", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_rejects_when_the_artifact_cannot_be_read_completely()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [],
            new UnreadableArtifactReader());

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.NotEmpty(decision.Diagnostics);
    }

    [Fact]
    public void Evaluate_rejects_a_completed_artifact_without_a_matching_current_write_trace()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");
        var artifactRef = BuildArtifactRef(runId, "code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
            [
                CreatePrimaryArtifactWriteTrace(artifactRef) with
                {
                    Succeeded = false,
                    FailureMessage = "The write failed."
                },
                CreatePrimaryArtifactWriteTrace($"{artifactRef}.stale")
            ],
            new FixedTextArtifactReader(artifactRef, "Status: Completed"));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("current execution", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact primary process artifact", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_rejects_an_artifact_without_canonical_status()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "scaffold-contract");
        var artifactRef = BuildArtifactRef(runId, "scaffold-contract");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, """
                # Scaffold contract

                ## Notes
                This step records the intended scaffold contract only.
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("canonical Status", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_rejects_an_invalid_status_value()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");
        var artifactRef = BuildArtifactRef(runId, "code-change");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, """
                # Feature implementation change set

                Status: InProgress  # Feature implementation change set
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("invalid Status field", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_rejects_a_status_only_blocked_artifact()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "qa-validation");
        var artifactRef = BuildArtifactRef(runId, "qa-validation");
        var evidence = CreateEvidence(
            context,
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
            [CreatePrimaryArtifactWriteTrace(artifactRef)],
            new FixedTextArtifactReader(artifactRef, """
                # QA validation

                Status: Blocked
                """));

        var decision = new ProcessAgentExecutionOutcomeRecoveryPolicy().Evaluate(evidence);

        Assert.Equal(AgentExecutionOutcomeRecoveryStatus.Rejected, decision.Status);
        Assert.Contains("without concrete blocker evidence", decision.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessStepOutcomeResult DeserializeOutcome(AgentExecutionOutcomeRecoveryDecision decision)
    {
        var outcome = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(
            decision.MachineOutputJson,
            AgentOutputJson.SerializerOptions);
        Assert.NotNull(outcome);
        return outcome!;
    }

    private static string BuildArtifactRef(Guid runId, string sourceId)
        => $"artifacts/process-runs/{runId:D}/steps/{sourceId}.md";

    private static AgentExecutionOutcomeRecoveryEvidence CreateEvidence(
        AgentRuntimeContextIntent context,
        AgentExecutionOutcomeFailureCause cause,
        IReadOnlyList<AgentToolInvocationTrace> currentExecutionToolTraces,
        IAgentExecutionRecoveryArtifactReader artifactReader,
        Type? outputType = null)
    {
        return new AgentExecutionOutcomeRecoveryEvidence(
            context,
            cause,
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            outputType ?? typeof(ProcessStepOutcomeResult),
            currentExecutionToolTraces,
            artifactReader);
    }

    private static AgentRuntimeContextIntent CreateGovernedProcessContext(
        Guid processRunId,
        string sourceId)
    {
        return new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: sourceId,
            ProcessRunId: processRunId.ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            TargetScope: "ExternalProductTargetMutable",
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            AllowsProductMutation: true,
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            AllowedOperations: ["MutateProductTarget", "WriteManagedProcessArtifacts"]);
    }

    private static AgentToolInvocationTrace CreatePrimaryArtifactWriteTrace(
        string primaryArtifactRef,
        string toolName = ToolContractCatalog.WorkspaceWriteFile)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentToolInvocationTrace(
            toolName,
            ToolInvocationClassification.Mutation,
            Sequence: 1,
            StartedAtUtc: timestamp,
            CompletedAtUtc: timestamp,
            Succeeded: true,
            FailureMessage: string.Empty)
        {
            Signature = $"{toolName}|path={primaryArtifactRef}",
            TargetPath = primaryArtifactRef
        };
    }

    private sealed class FixedTextArtifactReader(string expectedRelativePath, string fixedContent)
        : IAgentExecutionRecoveryArtifactReader
    {
        public bool TryReadCompleteTextFile(string relativeManagedPath, out string content)
        {
            if (string.Equals(relativeManagedPath, expectedRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                content = fixedContent;
                return true;
            }

            content = string.Empty;
            return false;
        }
    }

    private sealed class UnreadableArtifactReader : IAgentExecutionRecoveryArtifactReader
    {
        public bool TryReadCompleteTextFile(string relativeManagedPath, out string content)
        {
            content = string.Empty;
            return false;
        }
    }
}

using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFinalizerPolicyTests
{
    [Fact]
    public void Validate_fails_when_required_finalizer_is_missing()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();

        var result = validator.Validate(policy, []);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "agent.finalizer.missing");
    }

    [Fact]
    public void Validate_accepts_exactly_one_valid_finalizer_call()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();
        var invocation = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and validated."),
            Sequence: 1);

        var result = validator.Validate(policy, [invocation]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.MatchingInvocationCount);
        Assert.IsType<ProcessStepOutcomeResult>(result.Output);
        Assert.NotEmpty(result.RawOutputHash);
    }

    [Fact]
    public void Validate_fails_when_required_finalizer_is_called_multiple_times()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();
        var first = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "First decision."),
            Sequence: 1);
        var second = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Second decision."),
            Sequence: 2);

        var result = validator.Validate(policy, [first, second]);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.MatchingInvocationCount);
        Assert.Contains(result.Errors, error => error.Code == "agent.finalizer.multiple_calls");
    }

    [Fact]
    public void Validate_fails_when_finalizer_arguments_are_malformed()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();
        var invocation = new AgentFinalizerInvocation(
            policy.ToolName,
            "Review complete. The result is approved.",
            Sequence: 1);

        var result = validator.Validate(policy, [invocation]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "agent.output.malformed_json");
    }

    [Fact]
    public void Validate_ignores_assistant_text_when_finalizer_is_required()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();
        var unrelatedTextTool = new AgentFinalizerInvocation(
            "assistant_text",
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Assistant text is not a finalizer."),
            Sequence: 1);

        var result = validator.Validate(policy, [unrelatedTextTool]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "agent.finalizer.missing");
    }

    [Fact]
    public void ResolveMode_defaults_process_step_contract_to_shadow()
    {
        var run = CreateRun(metadataJson: "{}");

        var mode = AgentFinalizerPolicies.ResolveMode(
            run,
            AgentStructuredOutputContracts.ProcessStepOutcomeResult);

        Assert.Equal(AgentFinalizerMode.Shadow, mode);
    }

    [Fact]
    public void ResolveMode_honors_required_metadata()
    {
        var run = CreateRun(
            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.RequiredFinalizerModeValue}}"}""");

        var mode = AgentFinalizerPolicies.ResolveMode(
            run,
            AgentStructuredOutputContracts.ProcessStepOutcomeResult);

        Assert.Equal(AgentFinalizerMode.Required, mode);
    }

    [Fact]
    public void TryResolveForStructuredOutput_returns_explicit_finalizer_for_every_known_contract()
    {
        var resolvedPolicies = AgentStructuredOutputContracts.All
            .Select(contract => (
                Contract: contract,
                Resolved: AgentFinalizerPolicies.TryResolveForStructuredOutput(contract, out var policy),
                Policy: policy))
            .ToList();

        Assert.All(resolvedPolicies, item => Assert.True(item.Resolved, item.Contract.ContractKey));
        Assert.All(resolvedPolicies, item => Assert.NotEmpty(item.Policy.ToolName));
        Assert.Equal(
            resolvedPolicies.Count,
            resolvedPolicies.Select(item => item.Policy.ToolName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ExecutionInvocationMetadata_builds_required_finalizer_and_repair_policy()
    {
        var metadataJson = ExecutionInvocationMetadata.Build(
            null,
            new ExecutionInvocationPolicy(
                FinalizerMode: AgentFinalizerMode.Required,
                MaxStructuredOutputRepairAttempts: 9,
                RequireStructuredOutputValidation: true));

        using var document = JsonDocument.Parse(metadataJson);
        var root = document.RootElement;

        Assert.Equal(
            AgentFinalizerPolicies.RequiredFinalizerModeValue,
            root.GetProperty(AgentFinalizerPolicies.FinalizerModeMetadataKey).GetString());
        Assert.Equal(
            ExecutionInvocationMetadata.MaxRepairAttempts,
            root.GetProperty(ExecutionInvocationMetadata.MaxStructuredOutputRepairAttemptsMetadataKey).GetInt32());
        Assert.True(root.GetProperty(ExecutionInvocationMetadata.RequireStructuredOutputValidationMetadataKey).GetBoolean());
    }

    private static AgentFinalizerPolicy CreatePolicy()
    {
        return AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
            "submit_process_step_outcome",
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            "Final process-step outcome.");
    }

    private static string SerializeOutcome(
        ProcessStepOutcomeStatus status,
        string reason)
    {
        return JsonSerializer.Serialize(
            new ProcessStepOutcomeResult
            {
                Status = status,
                Reason = reason,
                EvidenceRefs = ["execution://run-001"],
                NextActions = status == ProcessStepOutcomeStatus.Completed
                    ? []
                    : ["Escalate the blocked outcome."]
            },
            AgentOutputJson.SerializerOptions);
    }

    private static ExecutionRunRecord CreateRun(string metadataJson)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            RequestedBy: "process-automation-dispatch",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: "run-001",
            ProcessStepId: "step-001");
    }
}

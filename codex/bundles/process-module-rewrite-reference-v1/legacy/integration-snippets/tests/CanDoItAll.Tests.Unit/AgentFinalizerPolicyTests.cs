using System.Text.Json;
using System.Reflection;
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
    public void SequenceValidator_accepts_finalizer_as_last_significant_tool()
    {
        var policy = CreatePolicy();
        var timestamp = DateTimeOffset.UtcNow;
        var traces = new[]
        {
            CreateToolTrace("workspace_dotnet_build", ToolInvocationClassification.Validation, 1, timestamp),
            CreateToolTrace(policy.ToolName, ToolInvocationClassification.Read, 2, timestamp)
        };

        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);

        Assert.True(result.Succeeded);
        Assert.True(result.TraceAvailable);
        Assert.Equal(2, result.FinalizerSequence);
        Assert.Empty(result.ViolatingToolInvocations);
    }

    [Fact]
    public void SequenceValidator_fails_when_validation_tool_runs_after_required_finalizer()
    {
        var policy = CreatePolicy();
        var timestamp = DateTimeOffset.UtcNow;
        var traces = new[]
        {
            CreateToolTrace(policy.ToolName, ToolInvocationClassification.Read, 1, timestamp),
            CreateToolTrace("workspace_dotnet_test", ToolInvocationClassification.Validation, 2, timestamp)
        };

        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);

        Assert.False(result.Succeeded);
        Assert.True(result.TraceAvailable);
        Assert.Contains(result.Errors, error => error.Code == "agent.finalizer.not_last");
        Assert.Contains(result.ViolatingToolInvocations, trace => trace.ToolName == "workspace_dotnet_test");
    }

    [Fact]
    public void SequenceValidator_fails_when_process_mutation_tool_runs_after_required_finalizer()
    {
        var policy = CreatePolicy();
        var timestamp = DateTimeOffset.UtcNow;
        var traces = new[]
        {
            CreateToolTrace(policy.ToolName, ToolInvocationClassification.Read, 1, timestamp),
            CreateToolTrace(
                AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord,
                AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord),
                2,
                timestamp)
        };

        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);

        Assert.False(result.Succeeded);
        Assert.True(result.TraceAvailable);
        Assert.Contains(result.Errors, error => error.Code == "agent.finalizer.not_last");
        Assert.Contains(
            result.ViolatingToolInvocations,
            trace => trace.ToolName == AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord &&
                     trace.Classification == ToolInvocationClassification.Mutation);
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
    public void ResolveMode_honors_shadow_metadata()
    {
        var run = CreateRun(
            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.ShadowFinalizerModeValue}}"}""");

        var mode = AgentFinalizerPolicies.ResolveMode(
            run,
            AgentStructuredOutputContracts.ProcessStepOutcomeResult);

        Assert.Equal(AgentFinalizerMode.Shadow, mode);
    }

    [Fact]
    public void ResolveMode_honors_disabled_metadata()
    {
        var run = CreateRun(
            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.DisabledFinalizerModeValue}}"}""");

        var mode = AgentFinalizerPolicies.ResolveMode(
            run,
            AgentStructuredOutputContracts.ProcessStepOutcomeResult);

        Assert.Equal(AgentFinalizerMode.Disabled, mode);
    }

    [Fact]
    public void TryResolveForStructuredOutput_returns_false_for_unknown_contract()
    {
        var unknownContract = AgentStructuredOutputContract.For<UnknownOutputContract>(
            "unknown_output_contract",
            "Unknown output.");

        var resolved = AgentFinalizerPolicies.TryResolveForStructuredOutput(unknownContract, out var policy);

        Assert.False(resolved);
        Assert.False(policy.IsRequired);
        Assert.Empty(policy.ToolName);
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

    [Fact]
    public void ExecutionInvocationMetadata_resolves_context_workspace_scope_for_trusted_process_run()
    {
        var projectId = Guid.Parse("90ad1937-b84e-41a6-8a90-4d09e88a552c");
        var metadataJson = ExecutionInvocationMetadata.Build(
            ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
                null,
                WorkspaceScopeDescriptor.Project(projectId.ToString("D"))),
            new ExecutionInvocationPolicy());
        var run = CreateRun(metadataJson);

        var scope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);

        Assert.NotNull(scope);
        Assert.Equal(WorkspaceScopeKind.Project, scope!.Kind);
        Assert.Equal(projectId.ToString("D"), scope.Key);
    }

    [Fact]
    public void ExecutionInvocationMetadata_ignores_context_workspace_scope_for_untrusted_run()
    {
        var metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            null,
            WorkspaceScopeDescriptor.Project("90ad1937-b84e-41a6-8a90-4d09e88a552c"));
        var run = CreateRun(metadataJson) with
        {
            RequestedByKind = "user",
            ProcessRunId = string.Empty,
            ProcessStepId = string.Empty
        };

        var scope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);

        Assert.Null(scope);
    }

    [Fact]
    public void ExecutionInvocationMetadata_resolves_project_structure_launch_agent_for_trusted_process_run()
    {
        var launchAgent = new ProjectStructureAgentIdentityDescriptor(
            "codex-sb05-tetris-e2e",
            "Codex SB05 Tetris E2E",
            "LUCYSPOWER",
            @"C:\repositories\CanDoItAll",
            "maf-processes-refactor",
            "session-001");
        var metadataJson = ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(null, launchAgent);
        var run = CreateRun(metadataJson);

        var resolved = ExecutionInvocationMetadata.ResolveProjectStructureLaunchAgent(run);

        Assert.NotNull(resolved);
        Assert.Equal(launchAgent.AgentId, resolved!.AgentId);
        Assert.Equal(launchAgent.AgentName, resolved.AgentName);
        Assert.Equal(launchAgent.MachineName, resolved.MachineName);
        Assert.Equal(launchAgent.RepositoryRoot, resolved.RepositoryRoot);
        Assert.Equal(launchAgent.BranchName, resolved.BranchName);
        Assert.Equal(launchAgent.SessionId, resolved.SessionId);
    }

    [Fact]
    public void ExecutionInvocationMetadata_ignores_project_structure_launch_agent_for_untrusted_run()
    {
        var metadataJson = ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(
            null,
            new ProjectStructureAgentIdentityDescriptor(
                "codex-sb05-tetris-e2e",
                "Codex SB05 Tetris E2E",
                "LUCYSPOWER",
                @"C:\repositories\CanDoItAll",
                "maf-processes-refactor",
                "session-001"));
        var run = CreateRun(metadataJson) with
        {
            RequestedByKind = "user",
            ProcessRunId = string.Empty,
            ProcessStepId = string.Empty
        };

        var resolved = ExecutionInvocationMetadata.ResolveProjectStructureLaunchAgent(run);

        Assert.Null(resolved);
    }

    [Fact]
    public void AgentFramework_runtime_options_include_context_workspace_scope_from_metadata()
    {
        var projectId = Guid.Parse("d3441d50-39c0-427a-976d-38f8c11e8312");
        var metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            null,
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")));
        var run = CreateRun(metadataJson);
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "CreateRuntimeExecutionOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateRuntimeExecutionOptions method was not found.");

        var options = Assert.IsType<AgentRuntimeExecutionOptions>(method.Invoke(null, [run, null, null]));

        Assert.NotNull(options.ContextWorkspaceScope);
        Assert.Equal(WorkspaceScopeKind.Project, options.ContextWorkspaceScope!.Kind);
        Assert.Equal(projectId.ToString("D"), options.ContextWorkspaceScope.Key);
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

    private static AgentToolInvocationTrace CreateToolTrace(
        string toolName,
        ToolInvocationClassification classification,
        int sequence,
        DateTimeOffset timestamp)
    {
        return new AgentToolInvocationTrace(
            toolName,
            classification,
            sequence,
            StartedAtUtc: timestamp,
            CompletedAtUtc: timestamp,
            Succeeded: true,
            FailureMessage: string.Empty);
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

    private sealed class UnknownOutputContract
    {
        public required string Value { get; init; }
    }
}

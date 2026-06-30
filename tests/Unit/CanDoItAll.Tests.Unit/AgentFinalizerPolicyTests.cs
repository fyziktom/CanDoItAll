using System.Text.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFinalizerPolicyTests
{
    [Fact]
    public void Process_step_artifact_recovery_creates_completed_outcome_from_primary_artifact()
    {
        var runId = Guid.NewGuid();
        var context = CreateGovernedProcessContext(runId, "code-change");

        var pathResolved = MafAgentRuntime.TryBuildCurrentStepPrimaryManagedArtifactPath(
            context,
            out var primaryArtifactRef,
            out var pathFailure);
        var recovered = MafAgentRuntime.TryCreateProcessStepOutcomeFromPrimaryArtifact(
            context,
            primaryArtifactRef,
            """
            # Feature implementation change set

            Status: completed

            ## Changed files
            - external-target/C/programovani/dotnet/output/src/App/App.csproj
            """,
            out var outcome,
            out var recoveryFailure);

        Assert.True(pathResolved, pathFailure);
        Assert.True(recovered, recoveryFailure);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, outcome.Status);
        Assert.Equal([primaryArtifactRef], outcome.EvidenceRefs);
        Assert.Empty(outcome.NextActions);
        Assert.Contains("provider timeout", outcome.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status: completed", outcome.HumanReadableSummaryMarkdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process_step_artifact_recovery_rejects_in_progress_artifact()
    {
        var context = CreateGovernedProcessContext(Guid.NewGuid(), "code-change");

        var recovered = MafAgentRuntime.TryCreateProcessStepOutcomeFromPrimaryArtifact(
            context,
            "artifacts/process-runs/11111111-1111-1111-1111-111111111111/steps/code-change.md",
            """
            # Feature implementation change set

            Status: in progress
            """,
            out _,
            out var failure);

        Assert.False(recovered);
        Assert.Contains("Status line", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process_step_artifact_recovery_rejects_unsafe_step_artifact_file_name()
    {
        var context = CreateGovernedProcessContext(Guid.NewGuid(), "../code-change");

        var resolved = MafAgentRuntime.TryBuildCurrentStepPrimaryManagedArtifactPath(
            context,
            out _,
            out var failure);

        Assert.False(resolved);
        Assert.Contains("safe artifact file name", failure, StringComparison.OrdinalIgnoreCase);
    }

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
    public void NormalizeRequired_selects_last_valid_required_finalizer_call()
    {
        var validator = new DefaultAgentFinalizerValidator();
        var policy = CreatePolicy();
        var first = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Blocked, "Earlier decision."),
            Sequence: 1);
        var second = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Final decision."),
            Sequence: 2);

        var rawResult = validator.Validate(policy, [first, second]);
        var normalized = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, [first, second]);
        var normalizedResult = validator.Validate(policy, normalized);

        Assert.False(rawResult.Succeeded);
        var invocation = Assert.Single(normalized);
        Assert.Equal(second, invocation);
        Assert.True(normalizedResult.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(normalizedResult.Output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output.Status);
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
    public void Required_finalizer_repair_options_are_constrained_to_finalizer_tool()
    {
        var policy = CreatePolicy();
        var unrelatedTool = AIFunctionFactory.Create(
            () => "ok",
            "workspace_read_file",
            "Test tool.");
        var finalizerTool = AIFunctionFactory.Create(
            (ProcessStepOutcomeResult result) => "captured",
            policy.ToolName,
            "Test finalizer tool.");
        var chatOptions = new ChatOptions
        {
            AllowMultipleToolCalls = true,
            ToolMode = ChatToolMode.Auto,
            Tools = [unrelatedTool, finalizerTool]
        };

        var resolvedTool = MafAgentRuntime.ResolveRequiredFinalizerTool(
            policy,
            [finalizerTool]);
        MafAgentRuntime.ConfigureRequiredFinalizerRepairChatOptions(
            chatOptions,
            policy,
            resolvedTool);
        var repairOptions = MafAgentRuntime.CreateRequiredFinalizerRepairRunOptions(policy, resolvedTool);

        Assert.Same(finalizerTool, resolvedTool);
        Assert.False(chatOptions.AllowMultipleToolCalls);
        Assert.NotNull(chatOptions.Tools);
        var repairTool = Assert.Single(chatOptions.Tools!);
        Assert.Equal(policy.ToolName, repairTool.Name);
        var configuredToolMode = Assert.IsType<RequiredChatToolMode>(chatOptions.ToolMode);
        Assert.Equal(policy.ToolName, configuredToolMode.RequiredFunctionName);
        Assert.DoesNotContain(chatOptions.Tools!, tool => tool.Name == unrelatedTool.Name);
        Assert.False(repairOptions.AllowBackgroundResponses);
        Assert.NotNull(repairOptions.ChatOptions);
        var repairChatOptions = repairOptions.ChatOptions!;
        Assert.False(repairChatOptions.AllowMultipleToolCalls);
        Assert.Contains(policy.OutputContract.ContractKey, repairChatOptions.Instructions, StringComparison.Ordinal);
        Assert.NotNull(repairChatOptions.Tools);
        var repairRunTool = Assert.Single(repairChatOptions.Tools!);
        Assert.Equal(policy.ToolName, repairRunTool.Name);
        var requiredToolMode = Assert.IsType<RequiredChatToolMode>(repairChatOptions.ToolMode);
        Assert.Equal(policy.ToolName, requiredToolMode.RequiredFunctionName);
    }

    [Fact]
    public void Required_finalizer_json_repair_normalizes_direct_and_wrapped_payloads()
    {
        var policy = CreatePolicy();
        const string directPayload = """
        {
          "status": "Completed",
          "reason": "Contract resolved.",
          "branchOutcomeKey": "",
          "branchOutcomeTitle": "",
          "evidenceRefs": [
            "artifacts/process-runs/run-1/steps/feature-intake.md"
          ],
          "nextActions": [],
          "humanReadableSummaryMarkdown": "Done."
        }
        """;
        var wrappedPayload = $$"""
        {
          "result": {{directPayload}}
        }
        """;

        var directResult = MafAgentRuntime.TryNormalizeFinalizerJsonRepairText(
            policy,
            directPayload,
            out var directJson,
            out var directFailure);
        var wrappedResult = MafAgentRuntime.TryNormalizeFinalizerJsonRepairText(
            policy,
            wrappedPayload,
            out var wrappedJson,
            out var wrappedFailure);

        Assert.True(directResult, directFailure);
        Assert.True(wrappedResult, wrappedFailure);
        Assert.Equal(directJson, wrappedJson);
        var output = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(directJson, AgentOutputJson.SerializerOptions);
        Assert.NotNull(output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output!.Status);
        Assert.Equal("Contract resolved.", output.Reason);
    }

    [Fact]
    public void Required_finalizer_json_repair_normalizes_process_step_string_array_objects()
    {
        var policy = CreatePolicy();
        const string payload = """
        {
          "result": {
            "status": "Completed",
            "reason": "Contract resolved.",
            "branchOutcomeKey": "",
            "branchOutcomeTitle": "",
            "evidenceRefs": [
              {
                "path": "artifacts/process-runs/run-1/steps/feature-intake.md",
                "kind": "managed artifact"
              }
            ],
            "nextActions": [
              {
                "owner": "solution-architect",
                "action": "Review the scoped delivery boundary."
              }
            ],
            "humanReadableSummaryMarkdown": "Done."
          }
        }
        """;

        var result = MafAgentRuntime.TryNormalizeFinalizerJsonRepairText(
            policy,
            payload,
            out var argumentsJson,
            out var failure);

        Assert.True(result, failure);
        var output = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(argumentsJson, AgentOutputJson.SerializerOptions);
        Assert.NotNull(output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output!.Status);
        var evidenceRef = Assert.Single(output.EvidenceRefs);
        Assert.Contains("path: artifacts/process-runs/run-1/steps/feature-intake.md", evidenceRef, StringComparison.Ordinal);
        var nextAction = Assert.Single(output.NextActions);
        Assert.Contains("owner: solution-architect", nextAction, StringComparison.Ordinal);
        Assert.Contains("action: Review the scoped delivery boundary.", nextAction, StringComparison.Ordinal);
    }

    [Fact]
    public void Required_finalizer_json_repair_normalizes_missing_reason_from_human_summary()
    {
        var policy = CreatePolicy();
        const string payload = """
        {
          "status": "Completed",
          "branchOutcomeKey": "",
          "branchOutcomeTitle": "",
          "evidenceRefs": [
            "artifacts/process-runs/run-1/steps/feature-intake.md"
          ],
          "nextActions": [],
          "humanReadableSummaryMarkdown": "Scope packet was produced and stored for downstream architecture review."
        }
        """;

        var result = MafAgentRuntime.TryNormalizeFinalizerJsonRepairText(
            policy,
            payload,
            out var argumentsJson,
            out var failure);

        Assert.True(result, failure);
        var validation = new DefaultAgentFinalizerValidator().Validate(
            policy,
            [new AgentFinalizerInvocation(policy.ToolName, argumentsJson, Sequence: 1)]);
        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("Scope packet was produced and stored for downstream architecture review.", output.Reason);
    }

    [Fact]
    public void Streamed_finalizer_recorder_captures_complete_later_chunk_for_same_call()
    {
        var policy = CreatePolicy();
        var recorder = new MafAgentRuntime.StreamedFinalizerInvocationRecorder(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required);
        const string outcomeJson = """
        {
          "status": "Completed",
          "reason": "Scope clarified.",
          "branchOutcomeKey": "",
          "branchOutcomeTitle": "",
          "evidenceRefs": [
            "artifacts/process-runs/run-1/steps/feature-intake.md"
          ],
          "nextActions": [],
          "humanReadableSummaryMarkdown": "Scope clarified."
        }
        """;

        recorder.Record(new FunctionCallContent("finalizer-1", policy.ToolName));
        recorder.Record(new FunctionCallContent(
            "finalizer-1",
            policy.ToolName,
            new Dictionary<string, object?>
            {
                ["result"] = outcomeJson
            }));

        var invocation = Assert.Single(recorder.SnapshotFinalizerInvocations());
        Assert.Equal(policy.ToolName, invocation.ToolName);
        Assert.Equal(2, invocation.Sequence);

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, [invocation]);

        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output.Status);
        Assert.Equal("Scope clarified.", output.Reason);
    }

    [Fact]
    public void Finalizer_capture_accepts_process_step_outcome_result_as_json_string_argument()
    {
        var policy = CreatePolicy();
        var capture = CreateFinalizerCapture(policy);
        var submitMethod = capture.GetType().GetMethod("SubmitProcessStepOutcome")
            ?? throw new InvalidOperationException("SubmitProcessStepOutcome method was not found.");
        var snapshotMethod = capture.GetType().GetMethod("Snapshot")
            ?? throw new InvalidOperationException("Snapshot method was not found.");
        var outcomeJson = SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Provider sent result as a JSON string.");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(outcomeJson));

        var response = submitMethod.Invoke(capture, [document.RootElement]);
        var snapshot = snapshotMethod.Invoke(capture, []);

        Assert.Equal("Process step outcome finalizer captured.", response);
        var invocations = Assert.IsAssignableFrom<IReadOnlyList<AgentFinalizerInvocation>>(snapshot);
        var invocation = Assert.Single(invocations);
        Assert.Equal(policy.ToolName, invocation.ToolName);
        var validation = new DefaultAgentFinalizerValidator().Validate(policy, invocations);
        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output.Status);
        Assert.Equal("Provider sent result as a JSON string.", output.Reason);
    }

    [Fact]
    public void Finalizer_capture_normalizes_json_string_argument_missing_reason()
    {
        var policy = CreatePolicy();
        var capture = CreateFinalizerCapture(policy);
        var submitMethod = capture.GetType().GetMethod("SubmitProcessStepOutcome")
            ?? throw new InvalidOperationException("SubmitProcessStepOutcome method was not found.");
        var snapshotMethod = capture.GetType().GetMethod("Snapshot")
            ?? throw new InvalidOperationException("Snapshot method was not found.");
        const string outcomeJson = """
        {
          "status": "Completed",
          "branchOutcomeKey": "",
          "branchOutcomeTitle": "",
          "evidenceRefs": [
            "artifacts/process-runs/run-1/steps/feature-intake.md"
          ],
          "nextActions": [],
          "humanReadableSummaryMarkdown": "Feature intake completed with current-run evidence."
        }
        """;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(outcomeJson));

        submitMethod.Invoke(capture, [document.RootElement]);
        var snapshot = snapshotMethod.Invoke(capture, []);

        var invocations = Assert.IsAssignableFrom<IReadOnlyList<AgentFinalizerInvocation>>(snapshot);
        var validation = new DefaultAgentFinalizerValidator().Validate(policy, invocations);
        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("Feature intake completed with current-run evidence.", output.Reason);
    }

    [Fact]
    public void Effective_finalizer_invocations_prefer_valid_json_repair_over_invalid_captured_attempt()
    {
        var policy = CreatePolicy();
        var invalidCaptured = new AgentFinalizerInvocation(
            policy.ToolName,
            "not-json",
            Sequence: 1);
        var synthesizedRepair = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Repair produced a valid outcome."),
            Sequence: 2);

        var effective = MafAgentRuntime.CreateEffectiveFinalizerInvocations(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required,
            [invalidCaptured],
            [],
            [],
            [synthesizedRepair]);

        var invocation = Assert.Single(effective);
        Assert.Equal(2, invocation.Sequence);

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, effective);

        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, output.Status);
    }

    [Fact]
    public void Effective_finalizer_invocations_collapse_repeated_valid_captured_calls_to_last_valid_call()
    {
        var policy = CreatePolicy();
        var first = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "First valid outcome."),
            Sequence: 1);
        var second = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Second valid outcome."),
            Sequence: 2);
        var third = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Third valid outcome."),
            Sequence: 3);

        var effective = MafAgentRuntime.CreateEffectiveFinalizerInvocations(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required,
            [first, second, third],
            [],
            [],
            []);

        var invocation = Assert.Single(effective);
        Assert.Equal(3, invocation.Sequence);

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, effective);

        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("Third valid outcome.", output.Reason);
    }

    [Fact]
    public void Required_finalizer_repair_prompts_bound_previous_assistant_text()
    {
        var policy = CreatePolicy();
        var previousText = "START-" + new string('x', 30_000) + "-TAIL";

        var toolRepairPrompt = MafAgentRuntime.BuildRequiredFinalizerRepairPrompt(policy, previousText);
        var jsonRepairPrompt = MafAgentRuntime.BuildRequiredFinalizerJsonRepairPrompt(policy, previousText);

        AssertBoundedRepairPrompt(toolRepairPrompt);
        AssertBoundedRepairPrompt(jsonRepairPrompt);
    }

    [Fact]
    public void Governed_process_steps_force_framework_managed_history_for_responses_provider()
    {
        var agent = CreateAgent(AgentChatHistoryMode.ProviderManaged);
        var provider = CreateProvider(
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false);
        var options = new AgentRuntimeExecutionOptions(
            StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            FinalizerMode: AgentFinalizerMode.Required,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 1,
            ContextIntent: AgentRuntimeContextIntent.Empty with
            {
                IsGovernedProcessStep = true
            });

        var useFrameworkHistory = MafAgentRuntime.ShouldUseFrameworkManagedHistory(
            agent,
            provider,
            options);

        Assert.True(useFrameworkHistory);
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
            "codex-project-structure-e2e",
            "Codex Project Structure E2E",
            "LUCYSPOWER",
            @"C:\repositories\CanDoItAll",
            "project-structure-runtime-refactor",
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
                "codex-project-structure-e2e",
                "Codex Project Structure E2E",
                "LUCYSPOWER",
                @"C:\repositories\CanDoItAll",
                "project-structure-runtime-refactor",
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

        var options = Assert.IsType<AgentRuntimeExecutionOptions>(method.Invoke(null, [run, null, null, Array.Empty<AgentRuntimeInputAttachment>()]));

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

    private static object CreateFinalizerCapture(AgentFinalizerPolicy policy)
    {
        var captureType = typeof(MafAgentRuntime).GetNestedType("FinalizerCapture", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FinalizerCapture nested type was not found.");
        return Activator.CreateInstance(
                captureType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [policy],
                culture: null)
            ?? throw new InvalidOperationException("FinalizerCapture instance was not created.");
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

    private static void AssertBoundedRepairPrompt(string prompt)
    {
        Assert.True(prompt.Length < 14_000, $"Repair prompt length was {prompt.Length:N0}.");
        Assert.Contains("START-", prompt, StringComparison.Ordinal);
        Assert.Contains("-TAIL", prompt, StringComparison.Ordinal);
        Assert.Contains("middle of previous assistant text omitted", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 20_000), prompt, StringComparison.Ordinal);
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

    private static AgentDefinition CreateAgent(
        AgentChatHistoryMode chatHistoryMode)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            "Agent",
            "Role",
            "Summary",
            "Instructions",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-5.4-mini",
            AgentWorkloadKind.General,
            chatHistoryMode,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static ProviderProfile CreateProvider(
        ProviderTransportKind transport,
        bool preferFrameworkManagedHistory)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5.4-mini",
            transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            preferFrameworkManagedHistory,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
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
            ScaffoldToolOnly: false,
            AllowsProductMutation: true,
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            AllowedOperations: ["MutateProductTarget", "WriteManagedProcessArtifacts"]);
    }

    private sealed class UnknownOutputContract
    {
        public required string Value { get; init; }
    }
}

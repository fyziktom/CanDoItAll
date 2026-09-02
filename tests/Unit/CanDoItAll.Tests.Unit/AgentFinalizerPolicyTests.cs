using System.Text.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFinalizerPolicyTests
{
    [Theory]
    [InlineData(WorkspaceFileLimits.MaxTextReadCharacters, true)]
    [InlineData(WorkspaceFileLimits.MaxTextReadCharacters + 1, false)]
    public void Process_step_artifact_finalizer_recovery_requires_a_complete_bounded_read(
        int artifactCharacters,
        bool expectedSuccess)
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("process-artifact-finalizer-recovery");
        try
        {
            const string artifactRef = "artifacts/process-runs/run-001/steps/implementation.md";
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                "run-001",
                "steps",
                "implementation.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllText(artifactPath, new string('x', artifactCharacters));

            var recovered = MafStreamingTurnExecutor.TryReadCompleteRecoveryArtifact(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                artifactRef,
                out var artifactMarkdown);

            Assert.Equal(expectedSuccess, recovered);
            Assert.Equal(
                expectedSuccess ? artifactCharacters : 0,
                artifactMarkdown.Length);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
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

        var resolvedTool = MafFinalizerDriver.ResolveRequiredFinalizerTool(
            policy,
            [finalizerTool]);
        MafFinalizerDriver.ConfigureRequiredFinalizerRepairChatOptions(
            chatOptions,
            policy,
            resolvedTool);
        var history = HistoryInvocationContext.Create(HistoryWorkload.Agent);
        var repairOptions = MafFinalizerDriver.CreateRequiredFinalizerRepairRunOptions(policy, resolvedTool, history);
        var jsonRepairOptions = MafFinalizerDriver.CreateRequiredFinalizerJsonRepairRunOptions(history);
        Assert.Same(history, ProviderHistoryChatContext.Read(repairOptions.ChatOptions));
        Assert.Same(history, ProviderHistoryChatContext.Read(jsonRepairOptions.ChatOptions));

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
    public void Required_finalizer_repair_is_requested_after_invalid_matching_attempt()
    {
        var options = new AgentRuntimeExecutionOptions(
            StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            FinalizerMode: AgentFinalizerMode.Required,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 1);
        var invalidAttempt = new AgentFinalizerInvocation(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            """{"status":"Completed","reason":"","evidenceRefs":[],"nextActions":[]}""",
            Sequence: 1);

        var shouldRepair = MafFinalizerDriver.ShouldRequestMissingRequiredFinalizerRepair(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required,
            options,
            [],
            [invalidAttempt],
            out var policy);

        Assert.True(shouldRepair);
        Assert.Equal(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            policy.ToolName);
    }

    [Fact]
    public void Required_finalizer_repair_is_not_requested_after_valid_matching_attempt()
    {
        var options = new AgentRuntimeExecutionOptions(
            StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            FinalizerMode: AgentFinalizerMode.Required,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 1);
        var validAttempt = new AgentFinalizerInvocation(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Validated."),
            Sequence: 1);

        var shouldRepair = MafFinalizerDriver.ShouldRequestMissingRequiredFinalizerRepair(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required,
            options,
            [],
            [validAttempt],
            out _);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void Provider_timeout_artifact_recovery_is_eligible_after_invalid_finalizer_attempt()
    {
        var policy = CreatePolicy();
        var invalidAttempt = new AgentFinalizerInvocation(
            policy.ToolName,
            """{"status":"Completed","reason":"","evidenceRefs":[],"nextActions":[]}""",
            Sequence: 1);
        var providerFailure = new InvalidOperationException(
            "Provider failed.",
            new TimeoutException("Provider stream exceeded its absolute deadline."));

        var shouldRecover = MafFinalizerDriver.ShouldAttemptProviderFailureArtifactRecovery(
            policy,
            [invalidAttempt],
            providerFailure);

        Assert.True(shouldRecover);
    }

    [Fact]
    public void Provider_failure_artifact_recovery_rejects_valid_finalizer_or_non_timeout_failure()
    {
        var policy = CreatePolicy();
        var validAttempt = new AgentFinalizerInvocation(
            policy.ToolName,
            SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Validated."),
            Sequence: 1);
        var invalidAttempt = new AgentFinalizerInvocation(
            policy.ToolName,
            """{"status":"Completed","reason":"","evidenceRefs":[],"nextActions":[]}""",
            Sequence: 2);

        Assert.False(MafFinalizerDriver.ShouldAttemptProviderFailureArtifactRecovery(
            policy,
            [validAttempt],
            new TimeoutException("Provider stream timed out.")));
        Assert.False(MafFinalizerDriver.ShouldAttemptProviderFailureArtifactRecovery(
            policy,
            [invalidAttempt],
            new InvalidOperationException("Provider failed without a timeout.")));
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

        var directResult = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
            policy,
            directPayload,
            out var directJson,
            out var directFailure);
        var wrappedResult = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
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

        var result = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
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

        var result = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
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
        var recorder = new MafFinalizerDriver.StreamedFinalizerInvocationRecorder(
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
        var outcomeJson = SerializeOutcome(ProcessStepOutcomeStatus.Completed, "Provider sent result as a JSON string.");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(outcomeJson));

        var response = capture.Submit(document.RootElement);
        var snapshot = capture.Snapshot();

        Assert.True(response.Succeeded);
        Assert.Equal("Process step outcome finalizer captured.", response.Message);
        var invocation = Assert.Single(snapshot);
        Assert.Equal(policy.ToolName, invocation.ToolName);
        var validation = new DefaultAgentFinalizerValidator().Validate(policy, snapshot);
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

        capture.Submit(document.RootElement);
        var snapshot = capture.Snapshot();

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, snapshot);
        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("Feature intake completed with current-run evidence.", output.Reason);
    }

    [Fact]
    public void Finalizer_capture_commits_first_valid_process_step_outcome_exactly_once()
    {
        var policy = CreatePolicy();
        var capture = CreateFinalizerCapture(policy);
        using var firstDocument = JsonDocument.Parse(SerializeOutcome(
            ProcessStepOutcomeStatus.Completed,
            "First valid governed outcome."));
        using var secondDocument = JsonDocument.Parse(SerializeOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Duplicate governed outcome."));

        var firstResponse = capture.Submit(firstDocument.RootElement);
        var secondResponse = capture.Submit(secondDocument.RootElement);
        var invocation = Assert.Single(capture.Snapshot());
        var validation = new DefaultAgentFinalizerValidator().Validate(policy, [invocation]);

        Assert.True(firstResponse.Succeeded);
        Assert.Equal("Process step outcome finalizer captured.", firstResponse.Message);
        Assert.True(secondResponse.Succeeded);
        Assert.Equal("Finalizer 'submit_process_step_outcome' already captured; duplicate submission ignored.", secondResponse.Message);
        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("First valid governed outcome.", output.Reason);
    }

    [Fact]
    public void Finalizer_capture_rejects_invalid_outcome_without_poisoning_valid_correction()
    {
        var policy = CreatePolicy();
        var capture = CreateFinalizerCapture(policy);
        var invalidOutcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Invalid completion without evidence.",
            EvidenceRefs = [],
            NextActions = []
        };
        using var invalidDocument = JsonDocument.Parse(JsonSerializer.Serialize(
            invalidOutcome,
            AgentOutputJson.SerializerOptions));
        using var validDocument = JsonDocument.Parse(SerializeOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Corrected completion with evidence."));

        var rejection = capture.Submit(invalidDocument.RootElement);

        Assert.False(rejection.Succeeded);
        Assert.Contains("process.step_outcome.completed_evidence_ref_required", rejection.Message, StringComparison.Ordinal);
        Assert.Empty(capture.Snapshot());

        capture.Submit(validDocument.RootElement);
        var invocation = Assert.Single(capture.Snapshot());
        var validation = new DefaultAgentFinalizerValidator().Validate(policy, [invocation]);

        Assert.True(validation.Succeeded);
        var output = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        Assert.Equal("Corrected completion with evidence.", output.Reason);
    }

    [Fact]
    public void Finalizer_capture_returns_recoverable_feedback_for_branch_title_without_key()
    {
        var policy = CreatePolicy();
        var capture = CreateFinalizerCapture(policy);
        var invalidOutcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Peer review completed.",
            BranchOutcomeKey = string.Empty,
            BranchOutcomeTitle = "Peer review approved",
            EvidenceRefs = ["artifacts/process-runs/run-1/steps/peer-review.md"],
            NextActions = []
        };
        using var invalidDocument = JsonDocument.Parse(JsonSerializer.Serialize(
            invalidOutcome,
            AgentOutputJson.SerializerOptions));
        using var correctedDocument = JsonDocument.Parse(SerializeOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Peer review completed with no branch selection."));

        var rejection = capture.Submit(invalidDocument.RootElement);

        Assert.Contains(
            "process.step_outcome.branch_key_required",
            rejection.Message,
            StringComparison.Ordinal);
        Assert.False(rejection.Succeeded);
        Assert.Empty(capture.Snapshot());

        capture.Submit(correctedDocument.RootElement);

        Assert.Single(capture.Snapshot());
    }

    [Fact]
    public void Required_finalizer_instructions_explain_branch_field_invariant()
    {
        var instructions = MafFinalizerDriver.BuildRequiredFinalizerArgumentInstructions(CreatePolicy());

        Assert.Contains(
            "`branchOutcomeTitle` requires a non-empty stable `branchOutcomeKey`",
            instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "both `branchOutcomeKey` and `branchOutcomeTitle` must be empty strings",
            instructions,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Process_step_finalizer_tool_schema_exposes_branch_field_invariant()
    {
        var capture = Assert.IsType<FinalizerCapture>(MafFinalizerToolFactory.CreateCapture(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required));
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(capture.Tools));
        var schema = function.JsonSchema.GetRawText();

        Assert.Contains(
            "branchOutcomeTitle requires a non-empty stable branchOutcomeKey",
            schema,
            StringComparison.Ordinal);
        Assert.Contains(
            "When no branch is selected, both branch fields must be empty strings",
            schema,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentFinalizerMode.Required, false, false)]
    [InlineData(AgentFinalizerMode.Required, true, false)]
    [InlineData(AgentFinalizerMode.Shadow, false, true)]
    [InlineData(AgentFinalizerMode.Disabled, false, true)]
    [InlineData(AgentFinalizerMode.Disabled, true, false)]
    public void Runtime_tool_call_policy_prevents_parallel_required_finalizers(
        AgentFinalizerMode finalizerMode,
        bool hasApprovalTools,
        bool expected)
    {
        Assert.Equal(
            expected,
            MafFinalizerDriver.ShouldAllowMultipleToolCalls(finalizerMode, hasApprovalTools));
    }

    [Theory]
    [InlineData(false, false, AgentFinalizerMode.Disabled, false, null)]
    [InlineData(true, true, AgentFinalizerMode.Disabled, false, true)]
    [InlineData(true, false, AgentFinalizerMode.Disabled, false, false)]
    [InlineData(true, true, AgentFinalizerMode.Disabled, true, false)]
    [InlineData(true, true, AgentFinalizerMode.Required, false, false)]
    public void Runtime_tool_call_policy_omits_parallel_option_without_tools(
        bool hasTools,
        bool supportsParallelFunctionTools,
        AgentFinalizerMode finalizerMode,
        bool hasApprovalTools,
        bool? expected)
    {
        Assert.Equal(
            expected,
            MafFinalizerDriver.ResolveAllowMultipleToolCalls(
                hasTools,
                supportsParallelFunctionTools,
                finalizerMode,
                hasApprovalTools));
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

        var effective = MafFinalizerDriver.CreateEffectiveFinalizerInvocations(
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

        var effective = MafFinalizerDriver.CreateEffectiveFinalizerInvocations(
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

        var toolRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerRepairPrompt(policy, previousText);
        var jsonRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerJsonRepairPrompt(policy, previousText);

        AssertBoundedRepairPrompt(toolRepairPrompt);
        AssertBoundedRepairPrompt(jsonRepairPrompt);
    }

    [Fact]
    public void Required_finalizer_repair_prompts_preserve_prior_tool_and_artifact_context()
    {
        var policy = CreatePolicy();
        var repairContext = string.Join(
            Environment.NewLine,
            "Previous turn tool calls observed by the provider:",
            "- Invoking tool 'workspace_read_file' with path=\"artifacts/process-runs/run-001/steps/feature-slice-intake.md\".",
            "Original governed process brief lines relevant to finalization:",
            "Primary write ref: artifacts/process-runs/run-001/steps/implementation-approach.md",
            "Completion rule: consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed.");

        var toolRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerRepairPrompt(policy, null, repairContext);
        var jsonRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerJsonRepairPrompt(policy, null, repairContext);

        Assert.Contains("Do not submit a generic no-prior-evidence blocker", toolRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", toolRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/run-001/steps/implementation-approach.md", toolRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("primary managed output alone was not written", toolRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("process runtime will materialize the canonical managed artifact", toolRepairPrompt, StringComparison.Ordinal);
        var toolRepairInstructions = MafFinalizerDriver.BuildRequiredFinalizerRepairInstructions(policy);
        Assert.Contains("never use this rule to waive product work", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("acceptanceCriteriaEvidence", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("criterionId", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("status", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("summary", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("evidenceRefs", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("aliases such as `id`, `passed`, or `proofRefs`", toolRepairInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not return a generic no-prior-evidence blocker", jsonRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", jsonRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/run-001/steps/implementation-approach.md", jsonRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("primary managed output alone was not written", jsonRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("process runtime will materialize the canonical managed artifact", jsonRepairPrompt, StringComparison.Ordinal);
        Assert.Contains("never use this rule to waive product work", MafFinalizerDriver.BuildRequiredFinalizerJsonRepairInstructions(policy), StringComparison.Ordinal);
        Assert.DoesNotContain("If completion is impossible because a required managed output was not written", toolRepairPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("If completion is impossible because a required managed output was not written", jsonRepairPrompt, StringComparison.Ordinal);
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

        var useFrameworkHistory = MafRuntimeSessionBuilder.ShouldUseFrameworkManagedHistory(
            agent,
            provider,
            options);

        Assert.True(useFrameworkHistory);
    }

    [Fact]
    public void Transient_application_context_forces_framework_managed_history()
    {
        var agent = CreateAgent(AgentChatHistoryMode.ProviderManaged);
        var provider = CreateProvider(
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false);
        var options = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: false,
            MaxStructuredOutputRepairAttempts: 0)
        {
            TransientContext = new AgentRuntimeTransientContext("Selected CRM account: 42")
        };

        Assert.True(MafRuntimeSessionBuilder.ShouldUseFrameworkManagedHistory(
            agent,
            provider,
            options));
    }

    [Fact]
    public async Task Contextual_approval_continuation_restores_provider_managed_session()
    {
        var agent = CreateAgent(AgentChatHistoryMode.ProviderManaged);
        var provider = CreateProvider(
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false);
        var session = new ChatSessionRecord(
            Guid.NewGuid(),
            agent.Id,
            "Contextual approval",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: """{"conversationId":"provider-conversation"}""",
            Messages: [],
            PendingApprovals:
            [
                new PendingToolApprovalRecord(
                    "approval-1",
                    "call-1",
                    "crm_update_partner",
                    "function",
                    "Update selected CRM partner",
                    "{}")
            ]);
        var options = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: false,
            MaxStructuredOutputRepairAttempts: 0)
        {
            TransientContext = new AgentRuntimeTransientContext("Selected CRM partner: 42")
        };
        var runtimeAgent = new RecordingSessionAgent();

        var restoredSession = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
            runtimeAgent,
            agent,
            provider,
            provider.DefaultModel,
            session,
            options,
            CancellationToken.None,
            isApprovalContinuation: true);

        Assert.NotNull(restoredSession);
        Assert.Equal(1, runtimeAgent.DeserializeSessionCallCount);
        Assert.Equal(0, runtimeAgent.CreateSessionCallCount);
        Assert.Equal(
            "provider-conversation",
            runtimeAgent.SerializedState.GetProperty("conversationId").GetString());
    }

    // Governed process-step provider override/ordering tests moved to ProcessExecutionProviderSelectionPolicyTests
    // (SB13): the logic now lives in CanDoItAll.Modules.Processes.ProcessExecutionProviderSelectionPolicy.

    [Fact]
    public void Approval_continuation_uses_the_provider_persisted_for_the_original_run()
    {
        var configuredProvider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Configured provider"
        };
        var governedProvider = CreateProvider(
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false) with
        {
            Name = "Governed provider"
        };
        var run = CreateRun("{}") with
        {
            ProviderProfileId = governedProvider.Id,
            ProviderName = governedProvider.Name,
            Model = OpenAiModelIds.Gpt56Luna
        };

        var resolvedProvider = AgentFrameworkWorkspaceExecutionService.ResolveContinuationProvider(
            run,
            configuredProvider,
            [configuredProvider, governedProvider]);

        Assert.Equal(governedProvider.Id, resolvedProvider.Id);
        Assert.Equal(ProviderTransportKind.Responses, resolvedProvider.Transport);
    }

    [Fact]
    public void Legacy_approval_continuation_resolves_the_recorded_provider_by_name()
    {
        var configuredProvider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Configured provider"
        };
        var governedProvider = CreateProvider(
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false) with
        {
            Name = "Governed provider"
        };
        var run = CreateRun("{}") with
        {
            ProviderProfileId = null,
            ProviderName = governedProvider.Name
        };

        var resolvedProvider = AgentFrameworkWorkspaceExecutionService.ResolveContinuationProvider(
            run,
            configuredProvider,
            [configuredProvider, governedProvider]);

        Assert.Equal(governedProvider.Id, resolvedProvider.Id);
    }

    [Fact]
    public void Provider_compatible_runtime_model_preserves_advertised_luna_during_profile_switch()
    {
        var agent = CreateAgent(AgentChatHistoryMode.FrameworkManaged) with
        {
            ProviderProfileId = Guid.NewGuid(),
            Model = OpenAiModelIds.Gpt56Luna
        };
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            SuggestedModels = [OpenAiModelIds.Gpt56Luna]
        };

        var model = AgentFrameworkWorkspaceExecutionService.ResolveProviderCompatibleRuntimeModel(
            agent,
            provider,
            agent.Model);

        Assert.Equal(OpenAiModelIds.Gpt56Luna, model);
    }

    [Fact]
    public void Provider_compatible_runtime_model_uses_target_default_for_unadvertised_model()
    {
        var agent = CreateAgent(AgentChatHistoryMode.FrameworkManaged) with
        {
            ProviderProfileId = Guid.NewGuid(),
            Model = "source-provider-model"
        };
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            DefaultModel = "target-provider-model",
            SuggestedModels = ["target-provider-model"]
        };

        var model = AgentFrameworkWorkspaceExecutionService.ResolveProviderCompatibleRuntimeModel(
            agent,
            provider,
            agent.Model);

        Assert.Equal(provider.DefaultModel, model);
    }

    [Fact]
    public void Effective_thinking_effort_prefers_supported_agent_override()
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.Medium)
        };

        var effort = MafModelParametersBuilder.ResolveEffectiveThinkingEffort(
            provider,
            "gpt-5.4-mini",
            AgentThinkingEffortPolicy.WriteAgentOverride(
                "{}",
                AgentReasoningEffortLevel.High));

        Assert.Equal(AgentReasoningEffortLevel.High, effort);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("custom-deployment-west")]
    public void Runtime_ignores_provider_default_for_unsupported_or_unknown_model(string model)
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.Medium)
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            model,
            requestedTemperature: null,
            forceOmitTemperature: false,
            agentConfigurationJson: "{}");

        Assert.Null(options.Reasoning);
        Assert.Null(options.RawRepresentationFactory);
    }

    [Theory]
    [InlineData("gpt-4.1", "does not support configurable thinking effort")]
    [InlineData("custom-deployment-west", "capability is not defined")]
    public void Runtime_rejects_agent_override_for_unsupported_or_unknown_model(
        string model,
        string expectedMessage)
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true);
        var agentConfiguration = AgentThinkingEffortPolicy.WriteAgentOverride(
            "{}",
            AgentReasoningEffortLevel.High);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MafModelParametersBuilder.CreateModelCompatibleChatOptions(
                provider,
                model,
                requestedTemperature: null,
                forceOmitTemperature: false,
                agentConfigurationJson: agentConfiguration));

        Assert.Contains("agent thinking-effort override", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
#pragma warning disable OPENAI001
    public void Max_reasoning_effort_builds_responses_native_OpenAI_options()
    {
        const ProviderTransportKind transport = ProviderTransportKind.Responses;
        var provider = CreateProvider(transport, preferFrameworkManagedHistory: false) with
        {
            ConfigurationJson = "{\"reasoningEffort\":\"max\"}"
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            OpenAiModelIds.Gpt56Luna,
            requestedTemperature: null,
            forceOmitTemperature: false);
        var rawOptions = Assert.IsAssignableFrom<object>(options.RawRepresentationFactory!(null!));

        var responseOptions = Assert.IsType<OpenAI.Responses.CreateResponseOptions>(rawOptions);
        Assert.Equal("max", responseOptions.ReasoningOptions!.ReasoningEffortLevel.ToString());
    }
#pragma warning restore OPENAI001

    [Theory]
    [InlineData(ProviderTransportKind.Responses)]
    [InlineData(ProviderTransportKind.ChatCompletions)]
#pragma warning disable OPENAI001
    public void Minimal_reasoning_effort_builds_transport_native_OpenAI_options(ProviderTransportKind transport)
    {
        var provider = CreateProvider(transport, preferFrameworkManagedHistory: false) with
        {
            ConfigurationJson = "{\"reasoningEffort\":\"minimal\"}"
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            "gpt-5",
            requestedTemperature: null,
            forceOmitTemperature: false);
        var rawOptions = Assert.IsAssignableFrom<object>(options.RawRepresentationFactory!(null!));

        if (transport == ProviderTransportKind.Responses)
        {
            var responseOptions = Assert.IsType<OpenAI.Responses.CreateResponseOptions>(rawOptions);
            Assert.Equal("minimal", responseOptions.ReasoningOptions!.ReasoningEffortLevel.ToString());
            return;
        }

        var chatOptions = Assert.IsType<OpenAI.Chat.ChatCompletionOptions>(rawOptions);
        Assert.Equal("minimal", chatOptions.ReasoningEffortLevel.ToString());
    }
#pragma warning restore OPENAI001

    [Fact]
    public void Azure_supported_deployment_omits_non_null_temperature_from_MAF_options()
    {
        const string deployment = "reasoning-deployment";
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Azure OpenAI",
            Kind = ProviderKind.AzureOpenAi,
            BaseUrl = "https://azure-openai.test",
            DefaultModel = deployment,
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    deployment,
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Defined,
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High])
            ]
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            deployment,
            requestedTemperature: 0.4f,
            forceOmitTemperature: false,
            agentConfigurationJson: AgentThinkingEffortPolicy.WriteAgentOverride(
                "{}",
                AgentReasoningEffortLevel.High));

        Assert.Null(options.Temperature);
        Assert.Equal(ReasoningEffort.High, options.Reasoning!.Effort);
    }

    [Fact]
    public void Ollama_binary_thinking_effort_builds_native_boolean_option()
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Ollama",
            Kind = ProviderKind.Ollama,
            BaseUrl = "http://ollama.test",
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = "qwen3.5:2b"
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            provider.DefaultModel,
            requestedTemperature: null,
            forceOmitTemperature: false,
            agentConfigurationJson: """{"modelParameters":{"reasoningEffort":"medium"}}""");

        Assert.True(Assert.IsType<bool>(options.AdditionalProperties![OllamaOption.Think.Name]));
    }

    [Fact]
    public void Ollama_gptoss_thinking_effort_builds_native_level_option()
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Ollama",
            Kind = ProviderKind.Ollama,
            BaseUrl = "http://ollama.test",
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = "gptoss32k:latest"
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            provider.DefaultModel,
            requestedTemperature: null,
            forceOmitTemperature: false,
            agentConfigurationJson: """{"modelParameters":{"reasoningEffort":"high"}}""");

        Assert.Equal(8192, options.MaxOutputTokens);
        Assert.Equal("high", options.AdditionalProperties![OllamaOption.Think.Name]);
    }

    [Fact]
    public void Ollama_provider_default_omits_native_thinking_option_when_unconfigured()
    {
        var provider = CreateProvider(
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Name = "Ollama",
            Kind = ProviderKind.Ollama,
            BaseUrl = "http://ollama.test",
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = "qwen3.5:2b"
        };

        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            provider.DefaultModel,
            requestedTemperature: null,
            forceOmitTemperature: false);

        Assert.False(options.AdditionalProperties?.ContainsKey(OllamaOption.Think.Name) ?? false);
    }

    [Fact]
    public void ExecutionInvocationMetadata_builds_required_finalizer_and_repair_policy()
    {
        var metadataJson = ExecutionInvocationMetadata.Build(
            null,
            new ExecutionInvocationPolicy(
                FinalizerMode: AgentFinalizerMode.Required,
                MaxStructuredOutputRepairAttempts: 9,
                RequireStructuredOutputValidation: true,
                AllowRequiredFinalizerStructuredOutputRecovery: true));

        using var document = JsonDocument.Parse(metadataJson);
        var root = document.RootElement;

        Assert.Equal(
            AgentFinalizerPolicies.RequiredFinalizerModeValue,
            root.GetProperty(AgentFinalizerPolicies.FinalizerModeMetadataKey).GetString());
        Assert.Equal(
            ExecutionInvocationMetadata.MaxRepairAttempts,
            root.GetProperty(ExecutionInvocationMetadata.MaxStructuredOutputRepairAttemptsMetadataKey).GetInt32());
        Assert.True(root.GetProperty(ExecutionInvocationMetadata.RequireStructuredOutputValidationMetadataKey).GetBoolean());
        Assert.True(root.GetProperty(
            ExecutionInvocationMetadata.AllowRequiredFinalizerStructuredOutputRecoveryMetadataKey).GetBoolean());
    }

    [Fact]
    public void Required_finalizer_structured_output_recovery_allows_only_missing_finalizer_when_explicitly_enabled()
    {
        var policy = AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName);
        var missing = AgentFinalizerValidationResult.Failure(
            policy,
            matchingInvocationCount: 0,
            rawOutputHash: "sha256:missing",
            new AgentOutputValidationError
            {
                Code = "agent.finalizer.missing",
                Message = "Required finalizer was not called.",
                Path = "$.finalizer"
            });
        var malformed = AgentFinalizerValidationResult.Failure(
            policy,
            matchingInvocationCount: 1,
            rawOutputHash: "sha256:malformed",
            new AgentOutputValidationError
            {
                Code = "agent.output.invalid",
                Message = "Finalizer arguments are malformed.",
                Path = "$.finalizer"
            });

        Assert.True(RequiredFinalizerStructuredOutputRecoveryPolicy.CanRecover(
            recoveryEnabled: true,
            finalizerMode: AgentFinalizerMode.Required,
            validation: missing));
        Assert.False(RequiredFinalizerStructuredOutputRecoveryPolicy.CanRecover(
            recoveryEnabled: false,
            finalizerMode: AgentFinalizerMode.Required,
            validation: missing));
        Assert.False(RequiredFinalizerStructuredOutputRecoveryPolicy.CanRecover(
            recoveryEnabled: true,
            finalizerMode: AgentFinalizerMode.Required,
            validation: malformed));
        Assert.False(RequiredFinalizerStructuredOutputRecoveryPolicy.CanRecover(
            recoveryEnabled: true,
            finalizerMode: AgentFinalizerMode.Shadow,
            validation: missing));
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
            "BuildRuntimeExecutionOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRuntimeExecutionOptions method was not found.");

        var options = Assert.IsType<AgentRuntimeExecutionOptions>(
            method.Invoke(
                null,
                [
                    run,
                    null,
                    null,
                    null,
                    Array.Empty<AgentRuntimeInputAttachment>(),
                    null,
                    null,
                    AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                        WorkspaceScopeDescriptor.Sandbox)
                ]));

        Assert.NotNull(options.ContextWorkspaceScope);
        Assert.Equal(WorkspaceScopeKind.Project, options.ContextWorkspaceScope!.Kind);
        Assert.Equal(projectId.ToString("D"), options.ContextWorkspaceScope.Key);
        Assert.Equal(options.ContextWorkspaceScope, options.ContextIntent?.WorkspaceScope);
    }

    [Fact]
    public void AgentFramework_runtime_options_and_audit_use_trusted_transient_scope_for_interactive_run()
    {
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var projectScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var run = CreateRun("{}") with
        {
            ChatSessionId = Guid.NewGuid(),
            SourceKind = "project-structure",
            SourceId = projectId.ToString("D"),
            RequestedBy = "floating-agent-chat",
            RequestedByKind = "interactive",
            ProcessRunId = string.Empty,
            ProcessStepId = string.Empty
        };
        var transientContext = new AgentRuntimeTransientContext(
            "Project structure context",
            projectScope);
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "BuildRuntimeExecutionOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRuntimeExecutionOptions method was not found.");

        var options = Assert.IsType<AgentRuntimeExecutionOptions>(
            method.Invoke(
                null,
                [
                    run,
                    null,
                    null,
                    null,
                    Array.Empty<AgentRuntimeInputAttachment>(),
                    null,
                    transientContext,
                    AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(projectScope)
                ]));

        Assert.Equal(projectScope, options.ContextWorkspaceScope);
        Assert.Equal(projectScope, options.ContextIntent?.WorkspaceScope);
        using (WorkspaceExecutionAuditContext.BeginScope(run, options.ContextWorkspaceScope))
        {
            Assert.Equal(projectScope, WorkspaceExecutionAuditContext.Current?.ContextWorkspaceScope);
        }
    }

    [Fact]
    public void AgentFramework_runtime_options_reject_conflicting_trusted_workspace_scopes()
    {
        var recordedScope = WorkspaceScopeDescriptor.Project("project-a");
        var transientScope = WorkspaceScopeDescriptor.Project("project-b");
        var run = CreateRun(
            ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
                "{}",
                recordedScope));
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "BuildRuntimeExecutionOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRuntimeExecutionOptions method was not found.");

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(
                null,
                [
                    run,
                    null,
                    null,
                    null,
                    Array.Empty<AgentRuntimeInputAttachment>(),
                    null,
                    new AgentRuntimeTransientContext(string.Empty, transientScope),
                    AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(recordedScope)
                ]));

        var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains(
            "conflicting trusted workspace scopes",
            innerException.Message,
            StringComparison.Ordinal);
    }

    private static AgentFinalizerPolicy CreatePolicy()
    {
        // Resolve through the real catalog (rather than hand-rolling a partial policy) so every metadata field
        // (ToolDescription, ResultParameterDescription, CaptureConfirmationMessage, RepairArgumentInstructions,
        // KnownOutputNormalizer) matches production exactly; SB13 moved that metadata onto the policy record.
        AgentFinalizerPolicies.TryResolveForStructuredOutput(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            out var policy);
        return policy;
    }

    private static FinalizerCapture CreateFinalizerCapture(AgentFinalizerPolicy policy)
        => new(policy);

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


    private sealed class UnknownOutputContract
    {
        public required string Value { get; init; }
    }

    private sealed class RecordingSessionAgent : AIAgent
    {
        public int CreateSessionCallCount { get; private set; }

        public int DeserializeSessionCallCount { get; private set; }

        public JsonElement SerializedState { get; private set; }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken = default)
        {
            CreateSessionCallCount++;
            return ValueTask.FromResult<AgentSession>(new RecordingSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            DeserializeSessionCallCount++;
            SerializedState = serializedState.Clone();
            return ValueTask.FromResult<AgentSession>(new RecordingSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private sealed class RecordingSession : AgentSession;
    }
}

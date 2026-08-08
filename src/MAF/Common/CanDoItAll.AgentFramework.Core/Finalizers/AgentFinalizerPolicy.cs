using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentFinalizerPolicy(
    bool IsRequired,
    string ToolName,
    AgentStructuredOutputContract OutputContract)
{
    public Type OutputType => OutputContract.OutputType;

    /// <summary>The AI tool description shown to the provider for this finalizer's submit tool.</summary>
    public string ToolDescription { get; init; } = string.Empty;

    /// <summary>
    /// Non-null only for contracts whose finalizer tool accepts a raw JSON element (tolerant capture) instead of a
    /// strictly typed parameter; supplies the schema description for the tool's <c>result</c> parameter.
    /// </summary>
    public string? ResultParameterDescription { get; init; }

    /// <summary>The message recorded when this finalizer's result is captured successfully.</summary>
    public string CaptureConfirmationMessage { get; init; } = string.Empty;

    /// <summary>Extra argument-shape repair guidance appended to bounded finalizer repair instructions.</summary>
    public string RepairArgumentInstructions { get; init; } = string.Empty;

    /// <summary>
    /// Optional tolerant JSON normalizer invoked before strict deserialization during bounded finalizer repair.
    /// Non-null only for contracts whose finalizer tool accepts a raw JSON element (see
    /// <see cref="ResultParameterDescription"/>).
    /// </summary>
    public Func<string, FinalizerOutputNormalizationResult>? KnownOutputNormalizer { get; init; }

    public static AgentFinalizerPolicy NotRequired { get; } =
        new(false, string.Empty, AgentStructuredOutputContracts.ProcessStepOutcomeResult);

    public static AgentFinalizerPolicy Required<TOutput>(
        string toolName,
        string schemaName = "",
        string schemaDescription = "",
        string toolDescription = "",
        string? resultParameterDescription = null,
        string captureConfirmationMessage = "",
        string repairArgumentInstructions = "",
        Func<string, FinalizerOutputNormalizationResult>? knownOutputNormalizer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new AgentFinalizerPolicy(
            true,
            toolName.Trim(),
            AgentStructuredOutputContract.For<TOutput>(schemaName, schemaDescription))
        {
            ToolDescription = toolDescription,
            ResultParameterDescription = resultParameterDescription,
            CaptureConfirmationMessage = captureConfirmationMessage,
            RepairArgumentInstructions = repairArgumentInstructions,
            KnownOutputNormalizer = knownOutputNormalizer
        };
    }
}

/// <summary>The outcome of a tolerant, contract-specific finalizer JSON normalization attempt.</summary>
public sealed record FinalizerOutputNormalizationResult(bool Succeeded, string ArgumentsJson, string FailureMessage)
{
    public static FinalizerOutputNormalizationResult Success(string argumentsJson)
        => new(true, argumentsJson, string.Empty);

    public static FinalizerOutputNormalizationResult Failure(string failureMessage)
        => new(false, string.Empty, failureMessage);
}

public static class AgentFinalizerPolicies
{
    public const string SubmitProcessStepOutcomeToolName = "submit_process_step_outcome";
    public const string SubmitCodeReviewResultToolName = "submit_code_review_result";
    public const string SubmitArchitectureReviewResultToolName = "submit_architecture_review_result";
    public const string SubmitImplementationPlanToolName = "submit_implementation_plan";
    public const string SubmitTestPlanToolName = "submit_test_plan";
    public const string SubmitToolExecutionDecisionToolName = "submit_tool_execution_decision";
    public const string SubmitProcessStatePatchToolName = "submit_process_state_patch";
    public const string SubmitHumanEscalationRequestToolName = "submit_human_escalation_request";
    public const string FinalizerModeMetadataKey = "agentFinalizerMode";
    public const string RequiredFinalizerModeValue = "required";
    public const string ShadowFinalizerModeValue = "shadow";
    public const string DisabledFinalizerModeValue = "disabled";

    // Moved verbatim from MafFinalizerToolFactory (MAF) so the generic finalizer tool mechanism no longer needs to
    // know the process-step outcome contract by name; only the Core policy catalog references it.
    private const string ProcessStepOutcomeResultParameterDescription =
        "Final governed process-step outcome. Include status, reason, branchOutcomeKey, branchOutcomeTitle, evidenceRefs, nextActions, and humanReadableSummaryMarkdown. " +
        "branchOutcomeTitle requires a non-empty stable branchOutcomeKey declared by the current process brief. When no branch is selected, both branch fields must be empty strings. " +
        "Completed outcomes require at least one concrete current-run evidence reference.";

    // Moved verbatim from MafFinalizerDriver.BuildRequiredFinalizerArgumentInstructions (MAF).
    private static readonly string ProcessStepOutcomeRepairArgumentInstructions =
        "- Pass exactly one `result` object argument to `submit_process_step_outcome`; do not pass scalar `result`, `status`, `reason`, or `evidenceRefs` as sibling arguments." + Environment.NewLine +
        "- The `result` object must include `status`, `reason`, `branchOutcomeKey`, `branchOutcomeTitle`, `evidenceRefs`, `nextActions`, and `humanReadableSummaryMarkdown`. Use `Completed`, `Blocked`, `Failed`, `WaitingApproval`, or `Refused` for `status`." + Environment.NewLine +
        "- Always include `acceptanceCriteriaEvidence` as an array. Every entry must use the exact property names `criterionId`, `status`, `summary`, and `evidenceRefs`; use `Passed`, `Failed`, or `NotVerified` for the entry `status`. Do not substitute aliases such as `id`, `passed`, or `proofRefs`." + Environment.NewLine +
        "- `branchOutcomeTitle` requires a non-empty stable `branchOutcomeKey`, and the key must be an exact branch declared by the current process brief. If this step does not select a branch, both `branchOutcomeKey` and `branchOutcomeTitle` must be empty strings; do not use placeholders such as `none`, `n/a`, or `completed`." + Environment.NewLine +
        "- Do not copy placeholder evidence values. Evidence refs must be exact current-run refs already created or observed during this turn." + Environment.NewLine +
        "- If `status` is `Completed`, `evidenceRefs` must contain at least one concrete current-run evidence reference. If no such evidence exists, return `Blocked` or `Failed` with a concrete `nextActions` entry instead of claiming completion." + Environment.NewLine +
        "- In this bounded repair turn, a missing configured primary managed output alone is not a blocker after all non-artifact work and required current-run proof succeeded. Submit the evidence-backed Completed outcome and let the process runtime materialize that canonical artifact before its completion gates; never use this rule to waive product work, validation, or required receipts." + Environment.NewLine;

    public static bool TryResolveForStructuredOutput(
        AgentStructuredOutputContract? structuredOutput,
        out AgentFinalizerPolicy policy)
    {
        policy = structuredOutput?.OutputType switch
        {
            Type type when type == typeof(ProcessStepOutcomeResult) => AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
                SubmitProcessStepOutcomeToolName,
                AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                "Final process-step outcome submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final process-step outcome exactly once as typed machine-readable arguments.",
                resultParameterDescription: ProcessStepOutcomeResultParameterDescription,
                captureConfirmationMessage: "Process step outcome finalizer captured.",
                repairArgumentInstructions: ProcessStepOutcomeRepairArgumentInstructions,
                knownOutputNormalizer: ProcessStepOutcomeResultJsonNormalizer.Normalize),
            Type type when type == typeof(CodeReviewResult) => AgentFinalizerPolicy.Required<CodeReviewResult>(
                SubmitCodeReviewResultToolName,
                AgentStructuredOutputContracts.CodeReviewResultKey,
                "Final code-review decision submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final code-review result exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Code review result finalizer captured."),
            Type type when type == typeof(ArchitectureReviewResult) => AgentFinalizerPolicy.Required<ArchitectureReviewResult>(
                SubmitArchitectureReviewResultToolName,
                AgentStructuredOutputContracts.ArchitectureReviewResultKey,
                "Final architecture-review decision submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final architecture-review result exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Architecture review result finalizer captured."),
            Type type when type == typeof(ImplementationPlanResult) => AgentFinalizerPolicy.Required<ImplementationPlanResult>(
                SubmitImplementationPlanToolName,
                AgentStructuredOutputContracts.ImplementationPlanResultKey,
                "Final implementation plan submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final implementation plan exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Implementation plan finalizer captured."),
            Type type when type == typeof(TestPlanResult) => AgentFinalizerPolicy.Required<TestPlanResult>(
                SubmitTestPlanToolName,
                AgentStructuredOutputContracts.TestPlanResultKey,
                "Final test plan submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final test plan exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Test plan finalizer captured."),
            Type type when type == typeof(ToolExecutionDecisionResult) => AgentFinalizerPolicy.Required<ToolExecutionDecisionResult>(
                SubmitToolExecutionDecisionToolName,
                AgentStructuredOutputContracts.ToolExecutionDecisionResultKey,
                "Final tool-execution decision submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final tool-execution decision exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Tool execution decision finalizer captured."),
            Type type when type == typeof(ProcessStatePatch) => AgentFinalizerPolicy.Required<ProcessStatePatch>(
                SubmitProcessStatePatchToolName,
                AgentStructuredOutputContracts.ProcessStatePatchKey,
                "Final process-state patch submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final process-state patch exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Process state patch finalizer captured."),
            Type type when type == typeof(HumanEscalationRequest) => AgentFinalizerPolicy.Required<HumanEscalationRequest>(
                SubmitHumanEscalationRequestToolName,
                AgentStructuredOutputContracts.HumanEscalationRequestKey,
                "Final human-escalation request submitted through the exact-once finalizer tool.",
                toolDescription: "Submits the final human-escalation request exactly once as typed machine-readable arguments.",
                captureConfirmationMessage: "Human escalation request finalizer captured."),
            _ => AgentFinalizerPolicy.NotRequired
        };

        return policy.IsRequired;
    }

    public static AgentFinalizerMode ResolveMode(
        ExecutionRunRecord run,
        AgentStructuredOutputContract? structuredOutput)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!TryResolveForStructuredOutput(structuredOutput, out _))
        {
            return AgentFinalizerMode.Disabled;
        }

        var configuredMode = TryReadConfiguredMode(run.MetadataJson);
        if (configuredMode.HasValue)
        {
            return configuredMode.Value;
        }

        return IsProcessScopedRun(run)
            ? AgentFinalizerMode.Shadow
            : AgentFinalizerMode.Disabled;
    }

    private static AgentFinalizerMode? TryReadConfiguredMode(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(FinalizerModeMetadataKey, out var modeElement) ||
                modeElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var configuredMode = modeElement.GetString()?.Trim();
            if (string.Equals(configuredMode, RequiredFinalizerModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return AgentFinalizerMode.Required;
            }

            if (string.Equals(configuredMode, ShadowFinalizerModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return AgentFinalizerMode.Shadow;
            }

            if (string.Equals(configuredMode, DisabledFinalizerModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return AgentFinalizerMode.Disabled;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsProcessScopedRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }

    public static string FormatMode(AgentFinalizerMode mode)
    {
        return mode switch
        {
            AgentFinalizerMode.Required => RequiredFinalizerModeValue,
            AgentFinalizerMode.Shadow => ShadowFinalizerModeValue,
            AgentFinalizerMode.Disabled => DisabledFinalizerModeValue,
            _ => DisabledFinalizerModeValue
        };
    }
}

public sealed record AgentFinalizerValidationResult(
    bool Succeeded,
    AgentFinalizerPolicy Policy,
    int MatchingInvocationCount,
    string RawOutputHash,
    IReadOnlyList<AgentOutputValidationError> Errors,
    object? Output)
{
    public static AgentFinalizerValidationResult Success(
        AgentFinalizerPolicy policy,
        string rawOutputHash,
        object? output)
    {
        return new AgentFinalizerValidationResult(
            true,
            policy,
            1,
            rawOutputHash,
            [],
            output);
    }

    public static AgentFinalizerValidationResult Failure(
        AgentFinalizerPolicy policy,
        int matchingInvocationCount,
        string rawOutputHash,
        params AgentOutputValidationError[] errors)
    {
        return new AgentFinalizerValidationResult(
            false,
            policy,
            matchingInvocationCount,
            rawOutputHash,
            errors,
            null);
    }
}

public sealed record AgentFinalizerSequenceValidationResult(
    bool Succeeded,
    bool TraceAvailable,
    int? FinalizerSequence,
    IReadOnlyList<AgentToolInvocationTrace> ViolatingToolInvocations,
    IReadOnlyList<AgentOutputValidationError> Errors)
{
    public static AgentFinalizerSequenceValidationResult Success(
        bool traceAvailable,
        int? finalizerSequence)
    {
        return new AgentFinalizerSequenceValidationResult(
            true,
            traceAvailable,
            finalizerSequence,
            [],
            []);
    }

    public static AgentFinalizerSequenceValidationResult Failure(
        bool traceAvailable,
        int? finalizerSequence,
        IReadOnlyList<AgentToolInvocationTrace> violatingToolInvocations,
        params AgentOutputValidationError[] errors)
    {
        return new AgentFinalizerSequenceValidationResult(
            false,
            traceAvailable,
            finalizerSequence,
            violatingToolInvocations,
            errors);
    }
}

public static class AgentFinalizerSequenceValidator
{
    public static AgentFinalizerSequenceValidationResult Validate(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(toolInvocationTraces);

        if (!policy.IsRequired)
        {
            return AgentFinalizerSequenceValidationResult.Success(
                traceAvailable: toolInvocationTraces.Count > 0,
                finalizerSequence: null);
        }

        if (toolInvocationTraces.Count == 0)
        {
            return AgentFinalizerSequenceValidationResult.Success(
                traceAvailable: false,
                finalizerSequence: null);
        }

        var finalizerTraces = toolInvocationTraces
            .Where(trace => string.Equals(trace.ToolName, policy.ToolName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(trace => trace.Sequence)
            .ToList();
        if (finalizerTraces.Count == 0)
        {
            return AgentFinalizerSequenceValidationResult.Failure(
                traceAvailable: true,
                finalizerSequence: null,
                violatingToolInvocations: [],
                Error(
                    "agent.finalizer.trace_missing",
                    $"Required finalizer tool '{policy.ToolName}' was validated but no matching ordered tool trace was reported.",
                    "$.finalizer.sequence"));
        }

        var finalizerSequence = finalizerTraces[^1].Sequence;
        var violatingToolInvocations = toolInvocationTraces
            .Where(trace => trace.Sequence > finalizerSequence)
            .Where(IsSignificantPostFinalizerTool)
            .OrderBy(trace => trace.Sequence)
            .ToList();
        if (violatingToolInvocations.Count == 0)
        {
            return AgentFinalizerSequenceValidationResult.Success(
                traceAvailable: true,
                finalizerSequence);
        }

        var toolList = string.Join(
            ", ",
            violatingToolInvocations.Select(trace => $"{trace.ToolName}#{trace.Sequence}"));
        return AgentFinalizerSequenceValidationResult.Failure(
            traceAvailable: true,
            finalizerSequence,
            violatingToolInvocations,
            Error(
                "agent.finalizer.not_last",
                $"Required finalizer tool '{policy.ToolName}' was followed by significant tool invocation(s): {toolList}.",
                "$.finalizer.sequence"));
    }

    private static bool IsSignificantPostFinalizerTool(AgentToolInvocationTrace trace)
    {
        return trace.Classification is ToolInvocationClassification.Mutation
            or ToolInvocationClassification.Validation
            or ToolInvocationClassification.HostedProviderNative
            or ToolInvocationClassification.LocalMcp
            or ToolInvocationClassification.HostedMcp;
    }

    private static AgentOutputValidationError Error(
        string code,
        string message,
        string path)
    {
        return new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        };
    }
}

public static class AgentFinalizerInvocationNormalizer
{
    public static IReadOnlyList<AgentFinalizerInvocation> NormalizeRequired(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations,
        IAgentOutputValidatorRegistry? outputValidatorRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(invocations);

        if (!policy.IsRequired || invocations.Count <= 1)
        {
            return invocations;
        }

        var validator = new DefaultAgentFinalizerValidator(outputValidatorRegistry);
        var validation = validator.Validate(policy, invocations);
        if (validation.Succeeded)
        {
            return invocations;
        }

        for (var index = invocations.Count - 1; index >= 0; index--)
        {
            var candidate = invocations[index];
            if (!string.Equals(candidate.ToolName, policy.ToolName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateValidation = validator.Validate(policy, [candidate]);
            if (candidateValidation.Succeeded && candidateValidation.Output is not null)
            {
                return [candidate];
            }
        }

        return invocations;
    }
}

public interface IAgentFinalizerValidator
{
    AgentFinalizerValidationResult Validate(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations);
}

public sealed class DefaultAgentFinalizerValidator(
    IAgentOutputValidatorRegistry? outputValidatorRegistry = null) : IAgentFinalizerValidator
{
    private readonly IAgentOutputValidatorRegistry outputValidatorRegistry =
        outputValidatorRegistry ?? DefaultAgentOutputValidatorRegistry.Instance;

    public AgentFinalizerValidationResult Validate(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(invocations);

        if (!policy.IsRequired)
        {
            return new AgentFinalizerValidationResult(
                true,
                policy,
                0,
                string.Empty,
                [],
                null);
        }

        var matchingInvocations = invocations
            .Where(invocation => string.Equals(invocation.ToolName, policy.ToolName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(invocation => invocation.Sequence)
            .ToList();

        if (matchingInvocations.Count == 0)
        {
            return AgentFinalizerValidationResult.Failure(
                policy,
                0,
                string.Empty,
                Error(
                    "agent.finalizer.missing",
                    $"Required finalizer tool '{policy.ToolName}' was not called.",
                    "$.finalizer"));
        }

        if (matchingInvocations.Count > 1)
        {
            return AgentFinalizerValidationResult.Failure(
                policy,
                matchingInvocations.Count,
                string.Empty,
                Error(
                    "agent.finalizer.multiple_calls",
                    $"Required finalizer tool '{policy.ToolName}' was called {matchingInvocations.Count} times; exactly one call is allowed.",
                    "$.finalizer"));
        }

        if (!outputValidatorRegistry.TryResolve(policy.OutputType, out var outputValidator))
        {
            return AgentFinalizerValidationResult.Failure(
                policy,
                matchingInvocations.Count,
                string.Empty,
                Error(
                    "agent.finalizer.validator_missing",
                    $"Required finalizer output type '{policy.OutputType.FullName}' does not have a registered validator.",
                    "$.finalizer"));
        }

        var invocation = matchingInvocations[0];
        var validation = outputValidator.DeserializeAndValidate(invocation.ArgumentsJson);
        if (!validation.Succeeded)
        {
            return new AgentFinalizerValidationResult(
                false,
                policy,
                1,
                validation.RawOutputHash,
                validation.Validation.Errors,
                null);
        }

        return AgentFinalizerValidationResult.Success(
            policy,
            validation.RawOutputHash,
            validation.Output);
    }

    private static AgentOutputValidationError Error(
        string code,
        string message,
        string path)
    {
        return new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        };
    }
}

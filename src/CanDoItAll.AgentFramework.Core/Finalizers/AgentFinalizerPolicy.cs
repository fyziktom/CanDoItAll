using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentFinalizerPolicy(
    bool IsRequired,
    string ToolName,
    AgentStructuredOutputContract OutputContract)
{
    public Type OutputType => OutputContract.OutputType;

    public static AgentFinalizerPolicy NotRequired { get; } =
        new(false, string.Empty, AgentStructuredOutputContracts.ProcessStepOutcomeResult);

    public static AgentFinalizerPolicy Required<TOutput>(
        string toolName,
        string schemaName = "",
        string schemaDescription = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new AgentFinalizerPolicy(
            true,
            toolName.Trim(),
            AgentStructuredOutputContract.For<TOutput>(schemaName, schemaDescription));
    }
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

    public static bool TryResolveForStructuredOutput(
        AgentStructuredOutputContract? structuredOutput,
        out AgentFinalizerPolicy policy)
    {
        policy = structuredOutput?.OutputType switch
        {
            Type type when type == typeof(ProcessStepOutcomeResult) => AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
                SubmitProcessStepOutcomeToolName,
                AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                "Final process-step outcome submitted through the exact-once finalizer tool."),
            Type type when type == typeof(CodeReviewResult) => AgentFinalizerPolicy.Required<CodeReviewResult>(
                SubmitCodeReviewResultToolName,
                AgentStructuredOutputContracts.CodeReviewResultKey,
                "Final code-review decision submitted through the exact-once finalizer tool."),
            Type type when type == typeof(ArchitectureReviewResult) => AgentFinalizerPolicy.Required<ArchitectureReviewResult>(
                SubmitArchitectureReviewResultToolName,
                AgentStructuredOutputContracts.ArchitectureReviewResultKey,
                "Final architecture-review decision submitted through the exact-once finalizer tool."),
            Type type when type == typeof(ImplementationPlanResult) => AgentFinalizerPolicy.Required<ImplementationPlanResult>(
                SubmitImplementationPlanToolName,
                AgentStructuredOutputContracts.ImplementationPlanResultKey,
                "Final implementation plan submitted through the exact-once finalizer tool."),
            Type type when type == typeof(TestPlanResult) => AgentFinalizerPolicy.Required<TestPlanResult>(
                SubmitTestPlanToolName,
                AgentStructuredOutputContracts.TestPlanResultKey,
                "Final test plan submitted through the exact-once finalizer tool."),
            Type type when type == typeof(ToolExecutionDecisionResult) => AgentFinalizerPolicy.Required<ToolExecutionDecisionResult>(
                SubmitToolExecutionDecisionToolName,
                AgentStructuredOutputContracts.ToolExecutionDecisionResultKey,
                "Final tool-execution decision submitted through the exact-once finalizer tool."),
            Type type when type == typeof(ProcessStatePatch) => AgentFinalizerPolicy.Required<ProcessStatePatch>(
                SubmitProcessStatePatchToolName,
                AgentStructuredOutputContracts.ProcessStatePatchKey,
                "Final process-state patch submitted through the exact-once finalizer tool."),
            Type type when type == typeof(HumanEscalationRequest) => AgentFinalizerPolicy.Required<HumanEscalationRequest>(
                SubmitHumanEscalationRequestToolName,
                AgentStructuredOutputContracts.HumanEscalationRequestKey,
                "Final human-escalation request submitted through the exact-once finalizer tool."),
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

public sealed record AgentFinalizerInvocation(
    string ToolName,
    string ArgumentsJson,
    int Sequence);

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

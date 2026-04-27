using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentFinalizerMode
{
    Disabled,
    Shadow,
    Required
}

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
    public const string FinalizerModeMetadataKey = "agentFinalizerMode";
    public const string RequiredFinalizerModeValue = "required";
    public const string ShadowFinalizerModeValue = "shadow";
    public const string DisabledFinalizerModeValue = "disabled";

    public static bool TryResolveForStructuredOutput(
        AgentStructuredOutputContract? structuredOutput,
        out AgentFinalizerPolicy policy)
    {
        if (structuredOutput?.OutputType == typeof(ProcessStepOutcomeResult))
        {
            policy = AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
                SubmitProcessStepOutcomeToolName,
                AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                "Final process-step outcome submitted through the exact-once finalizer tool.");
            return true;
        }

        policy = AgentFinalizerPolicy.NotRequired;
        return false;
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

        return IsProcessStepRun(run)
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

    private static bool IsProcessStepRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
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

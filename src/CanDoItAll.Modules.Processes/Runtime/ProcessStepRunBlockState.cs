using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepRunBlockState
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void Clear(ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        stepRun.BlockReasonCode = ProcessStepBlockReasonCode.None;
        stepRun.RecoveryOptionsJson = "[]";
    }

    public static void Apply(ProcessStepRun stepRun, string reason)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        var code = InferBlockReasonCode(reason);
        stepRun.BlockReasonCode = code;
        stepRun.RecoveryOptionsJson = SerializeRecoveryOptions(ResolveRecoveryOptions(code));
    }

    public static IReadOnlyList<ProcessStepRecoveryOption> ResolveRecoveryOptions(ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (string.IsNullOrWhiteSpace(stepRun.RecoveryOptionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProcessStepRecoveryOption>>(
                    stepRun.RecoveryOptionsJson,
                    SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsMissingUpstreamArtifactBlock(ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return stepRun.BlockReasonCode == ProcessStepBlockReasonCode.MissingUpstreamArtifact ||
               ProcessRuntimeProgressionPlanner.IsMissingUpstreamArtifactBlock(stepRun.BlockedReason);
    }

    internal static ProcessStepBlockReasonCode InferBlockReasonCode(string reason)
    {
        var normalized = (reason ?? string.Empty).Trim();
        if (ProcessRuntimeProgressionPlanner.IsMissingUpstreamArtifactBlock(normalized) ||
            ContainsAny(normalized, "missing required artifact", "required artifacts remain missing", "missing upstream artifact"))
        {
            return ProcessStepBlockReasonCode.MissingUpstreamArtifact;
        }

        if (ContainsAny(normalized, "policy", "permission", "denied", "external-target", "not authorized"))
        {
            return ProcessStepBlockReasonCode.PolicyDeniedExternalPath;
        }

        if (ContainsAny(normalized, "credential", "api key", "secret"))
        {
            return ProcessStepBlockReasonCode.MissingCredential;
        }

        if (ContainsAny(normalized, "tool", "capability gap", "unavailable"))
        {
            return ProcessStepBlockReasonCode.ToolUnavailable;
        }

        if (ContainsAny(normalized, "validation", "test failed", "build failed"))
        {
            return ProcessStepBlockReasonCode.ValidationFailed;
        }

        if (ContainsAny(normalized, "no progress", "repeated", "loop"))
        {
            return ProcessStepBlockReasonCode.NoProgress;
        }

        if (ContainsAny(normalized, "runtime invariant", "invariant violation"))
        {
            return ProcessStepBlockReasonCode.RuntimeInvariantViolation;
        }

        if (ContainsAny(normalized, "artifact contract", "required artifact expectation"))
        {
            return ProcessStepBlockReasonCode.ArtifactContractUnsatisfied;
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? ProcessStepBlockReasonCode.Unknown
            : ProcessStepBlockReasonCode.AgentExecutionFailed;
    }

    private static IReadOnlyList<ProcessStepRecoveryOption> ResolveRecoveryOptions(ProcessStepBlockReasonCode code)
    {
        return code switch
        {
            ProcessStepBlockReasonCode.MissingUpstreamArtifact =>
            [
                ProcessStepRecoveryOption.WaitForArtifactMaterialization,
                ProcessStepRecoveryOption.RecoverArtifactsOnly,
                ProcessStepRecoveryOption.HumanEscalation
            ],
            ProcessStepBlockReasonCode.PolicyDeniedExternalPath =>
            [
                ProcessStepRecoveryOption.HumanEscalation,
                ProcessStepRecoveryOption.ReworkContinuation
            ],
            ProcessStepBlockReasonCode.ValidationFailed =>
            [
                ProcessStepRecoveryOption.RerunValidation,
                ProcessStepRecoveryOption.RepairImplementation
            ],
            ProcessStepBlockReasonCode.RuntimeInvariantViolation =>
            [
                ProcessStepRecoveryOption.HumanEscalation,
                ProcessStepRecoveryOption.ReworkContinuation
            ],
            ProcessStepBlockReasonCode.NoProgress =>
            [
                ProcessStepRecoveryOption.FreshAgentSession,
                ProcessStepRecoveryOption.HumanEscalation
            ],
            ProcessStepBlockReasonCode.MissingCredential or ProcessStepBlockReasonCode.ToolUnavailable =>
            [
                ProcessStepRecoveryOption.HumanEscalation,
                ProcessStepRecoveryOption.RetryAgent
            ],
            _ =>
            [
                ProcessStepRecoveryOption.RetryAgent,
                ProcessStepRecoveryOption.HumanEscalation
            ]
        };
    }

    private static string SerializeRecoveryOptions(IReadOnlyList<ProcessStepRecoveryOption> options)
    {
        return JsonSerializer.Serialize(options, SerializerOptions);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessBlockStateClassification(
    ProcessStepBlockReasonCode ReasonCode,
    ProcessStepBlockCause? BlockCause,
    IReadOnlyList<ProcessStepRecoveryOption> RecoveryOptions);

internal static class ProcessBlockStateClassifier
{
    public static ProcessBlockStateClassification Classify(string reason, ProcessStepBlockCause? cause = null)
    {
        var blockCause = cause ?? InferBlockCause(reason);
        var code = blockCause.HasValue
            ? ResolveBlockReasonCode(blockCause.Value)
            : InferBlockReasonCode(reason);

        return new ProcessBlockStateClassification(code, blockCause, ResolveRecoveryOptions(code));
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
            ContainsAny(normalized, "missing upstream artifact"))
        {
            return ProcessStepBlockReasonCode.MissingUpstreamArtifact;
        }

        if (ContainsAny(
                normalized,
                "artifact contract",
                "required artifact expectation",
                "missing required artifact",
                "required artifacts remain missing",
                "required artifacts are recorded",
                "required artifact contract validation"))
        {
            return ProcessStepBlockReasonCode.ArtifactContractUnsatisfied;
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

        return string.IsNullOrWhiteSpace(normalized)
            ? ProcessStepBlockReasonCode.Unknown
            : ProcessStepBlockReasonCode.AgentExecutionFailed;
    }

    internal static ProcessStepBlockCause? InferBlockCause(string reason)
    {
        var code = InferBlockReasonCode(reason);
        return code switch
        {
            ProcessStepBlockReasonCode.MissingUpstreamArtifact => ProcessStepBlockCause.UpstreamInput,
            ProcessStepBlockReasonCode.ArtifactContractUnsatisfied => ProcessStepBlockCause.OwnOutput,
            ProcessStepBlockReasonCode.ValidationFailed or ProcessStepBlockReasonCode.RuntimeInvariantViolation => ProcessStepBlockCause.RuntimeEvidence,
            ProcessStepBlockReasonCode.PolicyDeniedExternalPath => ProcessStepBlockCause.PolicyDenied,
            _ => null
        };
    }

    internal static ProcessStepBlockReasonCode ResolveBlockReasonCode(ProcessStepBlockCause cause)
    {
        return cause switch
        {
            ProcessStepBlockCause.OwnOutput => ProcessStepBlockReasonCode.ArtifactContractUnsatisfied,
            ProcessStepBlockCause.UpstreamInput => ProcessStepBlockReasonCode.MissingUpstreamArtifact,
            ProcessStepBlockCause.RuntimeEvidence => ProcessStepBlockReasonCode.ValidationFailed,
            ProcessStepBlockCause.PolicyDenied => ProcessStepBlockReasonCode.PolicyDeniedExternalPath,
            _ => ProcessStepBlockReasonCode.Unknown
        };
    }

    internal static IReadOnlyList<ProcessStepRecoveryOption> ResolveRecoveryOptions(ProcessStepBlockReasonCode code)
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
            ProcessStepBlockReasonCode.ArtifactContractUnsatisfied =>
            [
                ProcessStepRecoveryOption.RecoverArtifactsOnly,
                ProcessStepRecoveryOption.RetryAgent,
                ProcessStepRecoveryOption.HumanEscalation
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

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

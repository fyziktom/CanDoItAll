using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessRecoveryRoutingRequest(
    ProcessStepBlockReasonCode BlockReasonCode,
    ProcessStepBlockCause? FailureOwnership,
    string Diagnostic,
    IReadOnlyList<ProcessStepRecoveryOption> AvailableActions,
    IReadOnlyList<ProcessRecoveryRoutingAttempt> RecentAttempts,
    string EvidenceFingerprint,
    bool HasNewEvidence);

internal sealed record ProcessRecoveryRoutingAttempt(
    ProcessStepRecoveryOption Action,
    string EvidenceFingerprint,
    DateTimeOffset OccurredAtUtc);

internal sealed record ProcessRecoveryRoutingDecision(
    ProcessStepBlockReasonCode BlockReasonCode,
    ProcessStepBlockCause? FailureOwnership,
    ProcessStepRecoveryOption NextAction,
    ProcessRecoveryClassification Classification,
    string Reason,
    IReadOnlyList<ProcessStepRecoveryOption> AvailableActions,
    string EvidenceFingerprint,
    bool IsNoProgressGuarded);

internal static class ProcessRecoveryRouter
{
    public static ProcessRecoveryRoutingDecision Route(ProcessRecoveryRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var availableActions = NormalizeAvailableActions(request.AvailableActions);
        var preferredAction = ResolvePreferredAction(
            request.BlockReasonCode,
            request.FailureOwnership,
            request.Diagnostic);
        var evidenceFingerprint = string.IsNullOrWhiteSpace(request.EvidenceFingerprint)
            ? BuildEvidenceFingerprint(request.BlockReasonCode, request.FailureOwnership, request.Diagnostic)
            : request.EvidenceFingerprint.Trim();
        var selectedAction = SelectAvailableAction(preferredAction, availableActions);
        var noProgressGuarded = IsRepeatedNoProgressWithoutNewEvidence(
            request,
            selectedAction,
            evidenceFingerprint);
        if (noProgressGuarded)
        {
            selectedAction = SelectAvailableAction(ProcessStepRecoveryOption.HumanEscalation, availableActions);
        }

        return new ProcessRecoveryRoutingDecision(
            request.BlockReasonCode,
            request.FailureOwnership,
            selectedAction,
            ResolveClassification(request.BlockReasonCode, selectedAction),
            BuildReason(request, preferredAction, selectedAction, noProgressGuarded),
            availableActions.Count == 0 ? [selectedAction] : availableActions,
            evidenceFingerprint,
            noProgressGuarded);
    }

    public static string BuildEvidenceFingerprint(
        ProcessStepBlockReasonCode blockReasonCode,
        ProcessStepBlockCause? failureOwnership,
        string diagnostic)
    {
        var normalized = string.Join(
            "|",
            blockReasonCode,
            failureOwnership?.ToString() ?? "None",
            NormalizeDiagnostic(diagnostic));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static IReadOnlyList<ProcessStepRecoveryOption> NormalizeAvailableActions(
        IReadOnlyList<ProcessStepRecoveryOption> availableActions)
    {
        ArgumentNullException.ThrowIfNull(availableActions);

        return availableActions
            .Where(action => action != ProcessStepRecoveryOption.None)
            .Distinct()
            .ToList();
    }

    private static ProcessStepRecoveryOption ResolvePreferredAction(
        ProcessStepBlockReasonCode blockReasonCode,
        ProcessStepBlockCause? failureOwnership,
        string diagnostic)
    {
        if (failureOwnership == ProcessStepBlockCause.PolicyDenied)
        {
            return ProcessStepRecoveryOption.HumanEscalation;
        }

        return blockReasonCode switch
        {
            ProcessStepBlockReasonCode.MissingUpstreamArtifact => ProcessStepRecoveryOption.WaitForArtifactMaterialization,
            ProcessStepBlockReasonCode.ArtifactContractUnsatisfied => ProcessStepRecoveryOption.RecoverArtifactsOnly,
            ProcessStepBlockReasonCode.ValidationFailed => ProcessStepRecoveryOption.RepairImplementation,
            ProcessStepBlockReasonCode.NoProgress => ProcessStepRecoveryOption.FreshAgentSession,
            ProcessStepBlockReasonCode.PolicyDeniedExternalPath or
            ProcessStepBlockReasonCode.MissingCredential or
            ProcessStepBlockReasonCode.ToolUnavailable or
            ProcessStepBlockReasonCode.RuntimeInvariantViolation or
            ProcessStepBlockReasonCode.CapabilityGap => ProcessStepRecoveryOption.HumanEscalation,
            ProcessStepBlockReasonCode.ManualRerun => ProcessStepRecoveryOption.ReworkContinuation,
            ProcessStepBlockReasonCode.AgentExecutionFailed => ProcessStepRecoveryOption.FreshAgentSession,
            _ when ContainsValidationRepairSignal(diagnostic) => ProcessStepRecoveryOption.RepairImplementation,
            _ => ProcessStepRecoveryOption.RetryAgent
        };
    }

    private static ProcessStepRecoveryOption SelectAvailableAction(
        ProcessStepRecoveryOption preferredAction,
        IReadOnlyList<ProcessStepRecoveryOption> availableActions)
    {
        if (preferredAction != ProcessStepRecoveryOption.None &&
            availableActions.Contains(preferredAction))
        {
            return preferredAction;
        }

        if (preferredAction != ProcessStepRecoveryOption.None &&
            availableActions.Count == 0)
        {
            return preferredAction;
        }

        return availableActions.FirstOrDefault(ProcessStepRecoveryOption.HumanEscalation);
    }

    private static bool IsRepeatedNoProgressWithoutNewEvidence(
        ProcessRecoveryRoutingRequest request,
        ProcessStepRecoveryOption selectedAction,
        string evidenceFingerprint)
    {
        return request.BlockReasonCode == ProcessStepBlockReasonCode.NoProgress &&
               !request.HasNewEvidence &&
               request.RecentAttempts.Any(attempt =>
                   attempt.Action == selectedAction &&
                   string.Equals(attempt.EvidenceFingerprint, evidenceFingerprint, StringComparison.Ordinal));
    }

    private static ProcessRecoveryClassification ResolveClassification(
        ProcessStepBlockReasonCode blockReasonCode,
        ProcessStepRecoveryOption selectedAction)
    {
        return selectedAction switch
        {
            ProcessStepRecoveryOption.WaitForArtifactMaterialization or
            ProcessStepRecoveryOption.RecoverArtifactsOnly => ProcessRecoveryClassification.MissingArtifact,
            ProcessStepRecoveryOption.FreshAgentSession => ProcessRecoveryClassification.ContextResetRetry,
            ProcessStepRecoveryOption.RepairImplementation or
            ProcessStepRecoveryOption.RerunValidation or
            ProcessStepRecoveryOption.ReworkContinuation => ProcessRecoveryClassification.ProviderRepairRetry,
            ProcessStepRecoveryOption.HumanEscalation => blockReasonCode == ProcessStepBlockReasonCode.NoProgress
                ? ProcessRecoveryClassification.ContextResetRetry
                : ProcessRecoveryClassification.ManualRerun,
            ProcessStepRecoveryOption.RetryAgent => ProcessRecoveryClassification.AutomaticRetry,
            _ => ProcessRecoveryClassification.None
        };
    }

    private static string BuildReason(
        ProcessRecoveryRoutingRequest request,
        ProcessStepRecoveryOption preferredAction,
        ProcessStepRecoveryOption selectedAction,
        bool noProgressGuarded)
    {
        if (noProgressGuarded)
        {
            return "Repeated no-progress recovery attempt matched the same evidence fingerprint without new evidence; routing to human escalation.";
        }

        var ownership = request.FailureOwnership?.ToString() ?? "Unspecified";
        if (selectedAction != preferredAction)
        {
            return $"Preferred recovery action {preferredAction} was not available for {request.BlockReasonCode} owned by {ownership}; selected {selectedAction}.";
        }

        return $"Selected {selectedAction} for {request.BlockReasonCode} owned by {ownership}.";
    }

    private static bool ContainsValidationRepairSignal(string diagnostic)
    {
        return !string.IsNullOrWhiteSpace(diagnostic) &&
               (diagnostic.Contains("build failed", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("test failed", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("validation failed", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("repair", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDiagnostic(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(diagnostic.Length);
        var previousWasWhitespace = false;
        foreach (var character in diagnostic.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}

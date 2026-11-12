using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public enum ProcessStrategyResultRefusal { None, InvalidReceipt, InvalidHostCapabilities }

public static class ProcessStrategyResultSnapshot {
    private const int MaximumHostCapabilities = 32;

    public static bool TryCapture(StrategyResultEnvelope? result, [NotNullWhen(true)] out StrategyResultEnvelope? snapshot,
        out ProcessStrategyResultRefusal refusal) {
        snapshot = null;
        refusal = ProcessStrategyResultRefusal.InvalidReceipt;
        if (result is null ||
                result.ProducedArtifacts is not { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } ||
                result.RequestedArtifacts is not { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } ||
                result.Diagnostics is not { Count: <= ProcessStrategyResultLimits.MaximumDiagnostics } ||
                result.ManagerSignals is not { Count: <= ProcessStrategyResultLimits.MaximumManagerSignals }) {
            return false;
        }
        var captured = result with {
            ProducedArtifacts = result.ProducedArtifacts.Take(ProcessStrategyResultLimits.MaximumArtifacts + 1).ToImmutableArray(),
            RequestedArtifacts = result.RequestedArtifacts.Take(ProcessStrategyResultLimits.MaximumArtifacts + 1).ToImmutableArray(),
            Diagnostics = result.Diagnostics.Take(ProcessStrategyResultLimits.MaximumDiagnostics + 1).ToImmutableArray(),
            ManagerSignals = result.ManagerSignals.Take(ProcessStrategyResultLimits.MaximumManagerSignals + 1).ToImmutableArray()
        };
        if (!IsValid(captured)) {
            return false;
        }
        refusal = ProcessStrategyResultRefusal.InvalidHostCapabilities;
        if (!TryCaptureHostEvidence(captured.HostCapabilityEvidence, out var host)) {
            return false;
        }
        snapshot = captured with { HostCapabilityEvidence = host };
        refusal = ProcessStrategyResultRefusal.None;
        return true;
    }

    internal static bool IsValid(StrategyResultEnvelope? result) {
        return result is not null &&
            ProcessStrategyReceiptValuePolicy.IsStableIdentifier(result.StrategyId.Value) &&
            ProcessStrategyReceiptValuePolicy.IsStableVersion(result.StrategyVersion) &&
            result.IdempotencyKey != Guid.Empty && Enum.IsDefined(result.Outcome) &&
            (result.ExecutionRunId is null || result.ExecutionRunId is { Value: var execution } && execution != Guid.Empty) &&
            ProcessStrategyReceiptValuePolicy.IsSha256Digest(result.ResultHash) &&
            ProcessPublicReceiptTextPolicy.IsSafe(result.UserSafeSummary, ProcessStrategyResultLimits.MaximumUserSafeSummaryLength) &&
            result.ProducedArtifacts is { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } &&
            result.ProducedArtifacts.All(artifact => artifact is not null && artifact.ArtifactId.Value != Guid.Empty &&
                artifact.SlotId.Value != Guid.Empty && ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.ContentHash)) &&
            result.RequestedArtifacts is { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } &&
            result.RequestedArtifacts.All(artifact => artifact is not null && artifact.SlotId.Value != Guid.Empty &&
                ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.RequestHash)) &&
            result.Diagnostics is { Count: <= ProcessStrategyResultLimits.MaximumDiagnostics } &&
            result.Diagnostics.All(IsValidDiagnostic) &&
            result.ManagerSignals is { Count: <= ProcessStrategyResultLimits.MaximumManagerSignals } &&
            result.ManagerSignals.All(signal => signal is not null && ProcessStrategyReceiptValuePolicy.IsStableIdentifier(signal.Code.Value) &&
                ProcessStrategyReceiptValuePolicy.IsSha256Digest(signal.SignalHash) &&
                ProcessPublicReceiptTextPolicy.IsSafe(signal.SafeSummary, ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength));
    }

    private static bool IsValidDiagnostic(StrategyDiagnosticRef diagnostic) => diagnostic is not null &&
        ProcessStrategyReceiptValuePolicy.IsStableIdentifier(diagnostic.Code.Value) &&
        Enum.IsDefined(diagnostic.Sensitivity) &&
        ProcessStrategyReceiptValuePolicy.IsSha256Digest(diagnostic.EvidenceHash) &&
        ProcessPublicReceiptTextPolicy.IsSafe(diagnostic.SafeSummary, ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength) &&
        ProcessStrategyReceiptValuePolicy.IsRestrictedEvidenceReference(diagnostic.RestrictedEvidenceReference) &&
        Enum.IsDefined(diagnostic.RetrySafety) && Enum.IsDefined(diagnostic.Idempotency) &&
        (diagnostic.ExecutionSafetyAttestation is null || diagnostic.ExecutionSafetyAttestation.IsStructurallyValid());

    private static bool TryCaptureHostEvidence(ProcessHostCapabilityEvaluationEvidence? evidence,
        out ProcessHostCapabilityEvaluationEvidence? captured) {
        captured = null;
        if (evidence is null) {
            return true;
        }
        if (!IsBoundedHostIdentifier(evidence.ProfileId.Value) ||
                evidence.Capabilities is not { Count: <= MaximumHostCapabilities }) {
            return false;
        }
        var capabilities = evidence.Capabilities.Take(MaximumHostCapabilities + 1).ToImmutableArray();
        if (capabilities.Length > MaximumHostCapabilities || capabilities.Any(capability => capability is null ||
                !IsBoundedHostIdentifier(capability.Id.Value) || !capability.IsStructurallyValid()) ||
                capabilities.Select(capability => capability.Id).Distinct().Count() != capabilities.Length) {
            return false;
        }
        captured = new(evidence.ProfileId, capabilities.OrderBy(capability => capability.Id.Value, StringComparer.Ordinal).ToImmutableArray());
        return true;
    }

    private static bool IsBoundedHostIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= ProcessStrategyResultLimits.MaximumIdentifierLength;
}

using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Explicit MAF compatibility rule set for restoring a persisted <see cref="RuntimeStateEnvelope"/>.
/// Every rule returns a named, log-safe outcome — never a silent reset or replay. Order matters:
/// adapter/schema range is checked first (a completely foreign envelope never reaches the
/// fingerprint comparisons), then the explicit adapter package range, then provider/model
/// identity, then the effective history mode, then the versioned fingerprint dimensions.
/// Schema v1 envelopes compare their names-only toolset fingerprint against the current
/// legacy digest and carry no policy-split fingerprints; schema v2 envelopes compare the
/// tool-contract fingerprint plus the separated authority-policy, capability-policy, and
/// model-context dimensions. Missing state and legacy state are handled before any envelope
/// fields exist to compare.
/// </summary>
internal sealed class MafRuntimeStateCompatibilityPolicy : IRuntimeStateCompatibilityPolicy
{
    internal const string LegacyWrapMigrationId = "legacy-v0-wrap-v1";
    internal const string EnvelopeV1ReadMigrationId = "envelope-v1-names-toolset-read";

    public RuntimeStateCompatibilityDecision Evaluate(RuntimeStateCompatibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Envelope is null)
        {
            if (request.IsLegacyUnversionedState)
            {
                return new RuntimeStateCompatibilityDecision(
                    RuntimeStateCompatibilityOutcome.RegisteredMigration,
                    "Legacy unversioned Microsoft Agent Framework session state was found and is parseable; wrapping it as a versioned envelope for this restore.",
                    LegacyWrapMigrationId);
            }

            if (request.HasUnparseableStoredState)
            {
                // Fail closed: a stored-but-unparseable payload is never treated as "nothing was
                // stored" — that would silently discard state a caller might still need to know
                // about (for example approval continuation, which must fail closed rather than
                // replay a canonical transcript when its provider-side approval binding is lost).
                return new RuntimeStateCompatibilityDecision(
                    RuntimeStateCompatibilityOutcome.Incompatible,
                    "The persisted runtime state is not valid JSON and cannot be parsed as either a versioned envelope or legacy session state.");
            }

            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.SafeCanonicalReplay,
                "No runtime-state payload is present for this conversation; canonical transcript replay is registered for its history mode.");
        }

        var envelope = request.Envelope;
        if (!envelope.IsCompatibleWith(RuntimeStateAdapterIds.Maf, minimumSchemaVersion: 1, RuntimeStateEnvelope.CurrentSchemaVersion))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                $"Envelope adapter '{envelope.AdapterId}' schema {envelope.SchemaVersion} is outside the readable range for adapter '{RuntimeStateAdapterIds.Maf}' (1-{RuntimeStateEnvelope.CurrentSchemaVersion}).");
        }

        if (!IsAdapterPackageWithinCompatibilityRange(
                envelope.AdapterPackageVersion,
                request.CurrentAdapterPackageVersion,
                out var packageRangeReason))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                packageRangeReason);
        }

        if (envelope.ProviderProfileId != request.CurrentProviderProfileId ||
            envelope.ProviderTransport != request.CurrentProviderTransport)
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                $"Provider identity changed since this state was captured (captured provider '{envelope.ProviderProfileId:N}'/{envelope.ProviderTransport}, current provider '{request.CurrentProviderProfileId:N}'/{request.CurrentProviderTransport}).");
        }

        if (!string.Equals(envelope.Model, request.CurrentModel, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                $"Model changed since this state was captured (captured '{envelope.Model}', current '{request.CurrentModel}').");
        }

        // Effective history mode: state captured under a different history
        // mode is never restored as-is. Envelopes written before the field
        // existed omit it; absence stays tolerated rather than widening or
        // narrowing eligibility retroactively.
        if (envelope.HistoryMode is { } capturedHistoryMode &&
            request.CurrentHistoryMode is { } currentHistoryMode &&
            capturedHistoryMode != currentHistoryMode)
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                $"The effective chat-history mode changed since this state was captured (captured {capturedHistoryMode}, current {currentHistoryMode}).");
        }

        if (envelope.SchemaVersion == 1)
        {
            return EvaluateSchemaV1(envelope, request);
        }

        return EvaluateSchemaV2(envelope, request);
    }

    private static RuntimeStateCompatibilityDecision EvaluateSchemaV1(
        RuntimeStateEnvelope envelope,
        RuntimeStateCompatibilityRequest request)
    {
        // v1 toolset fingerprints hash tool names only. Comparing them against
        // the current names-only digest keeps conversations captured before
        // the contract hash restorable; the next persist stamps schema v2.
        if (!string.Equals(envelope.ToolsetFingerprint, request.CurrentLegacyToolsetNameFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The composed runtime toolset no longer matches the tool names captured with this schema-v1 state.");
        }

        if (!string.Equals(envelope.ContextPolicyFingerprint, request.CurrentContextPolicyFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The model-context digest (context-policy fingerprint) no longer matches the digest captured with this schema-v1 state.");
        }

        return new RuntimeStateCompatibilityDecision(
            RuntimeStateCompatibilityOutcome.CompatibleRestore,
            "Schema-v1 envelope matches the current run by provider, model, history mode, tool names, and model-context digest; restoring and re-stamping as schema v2 on the next persist.",
            EnvelopeV1ReadMigrationId);
    }

    private static RuntimeStateCompatibilityDecision EvaluateSchemaV2(
        RuntimeStateEnvelope envelope,
        RuntimeStateCompatibilityRequest request)
    {
        if (!string.Equals(envelope.ToolsetFingerprint, request.CurrentToolsetFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The composed runtime toolset contracts (names, schemas, classifications, approval wrappers) no longer match the contracts captured with this state.");
        }

        if (!string.Equals(envelope.ContextPolicyFingerprint, request.CurrentContextPolicyFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The model-context digest (context-policy fingerprint) no longer matches the digest captured with this state.");
        }

        if (!string.Equals(envelope.AuthorityPolicyFingerprint, request.CurrentAuthorityPolicyFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The admitted execution authority policy changed since this state was captured.");
        }

        if (!string.Equals(envelope.CapabilityPolicyFingerprint, request.CurrentCapabilityPolicyFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The effectively exposed capability set changed since this state was captured.");
        }

        return new RuntimeStateCompatibilityDecision(
            RuntimeStateCompatibilityOutcome.CompatibleRestore,
            "Envelope adapter, schema, package range, provider, model, history mode, tool contracts, authority policy, capability policy, and model-context digest all match the current run.");
    }

    /// <summary>
    /// Explicit adapter package compatibility range: the same major version is
    /// compatible; a different major version fails closed. When either side's
    /// version is unavailable or unparseable, the range check is skipped and
    /// the fingerprint dimensions above remain the deciding rules.
    /// </summary>
    internal static bool IsAdapterPackageWithinCompatibilityRange(
        string capturedVersion,
        string currentVersion,
        out string incompatibleReason)
    {
        incompatibleReason = string.Empty;
        if (!TryParseMajor(capturedVersion, out var capturedMajor) ||
            !TryParseMajor(currentVersion, out var currentMajor))
        {
            return true;
        }

        if (capturedMajor == currentMajor)
        {
            return true;
        }

        incompatibleReason =
            $"The Microsoft Agent Framework adapter package major version changed since this state was captured (captured '{capturedVersion}', current '{currentVersion}').";
        return false;
    }

    private static bool TryParseMajor(string version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var span = version.AsSpan().Trim();
        var end = 0;
        while (end < span.Length && char.IsAsciiDigit(span[end]))
        {
            end++;
        }

        return end > 0 && int.TryParse(span[..end], out major);
    }
}

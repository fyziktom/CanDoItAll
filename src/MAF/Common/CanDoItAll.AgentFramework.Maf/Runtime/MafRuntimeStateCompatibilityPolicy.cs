using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Explicit MAF compatibility rule set for restoring a persisted <see cref="RuntimeStateEnvelope"/>.
/// Every rule returns a named, log-safe outcome — never a silent reset or replay. Order matters:
/// adapter/schema range is checked first (a completely foreign envelope never reaches the
/// fingerprint comparisons), then provider/model identity, then toolset/context-policy
/// fingerprints. Missing state and legacy state are handled before any envelope fields exist to
/// compare.
/// </summary>
internal sealed class MafRuntimeStateCompatibilityPolicy : IRuntimeStateCompatibilityPolicy
{
    internal const string LegacyWrapMigrationId = "legacy-v0-wrap-v1";

    public RuntimeStateCompatibilityDecision Evaluate(RuntimeStateCompatibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Envelope is null)
        {
            if (request.IsLegacyUnversionedState)
            {
                return new RuntimeStateCompatibilityDecision(
                    RuntimeStateCompatibilityOutcome.RegisteredMigration,
                    "Legacy unversioned Microsoft Agent Framework session state was found and is parseable; wrapping it as schema v1 for this restore.",
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

        if (!string.Equals(envelope.ToolsetFingerprint, request.CurrentToolsetFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The composed runtime toolset fingerprint no longer matches the fingerprint captured with this state.");
        }

        if (!string.Equals(envelope.ContextPolicyFingerprint, request.CurrentContextPolicyFingerprint, StringComparison.Ordinal))
        {
            return new RuntimeStateCompatibilityDecision(
                RuntimeStateCompatibilityOutcome.Incompatible,
                "The context-policy fingerprint no longer matches the fingerprint captured with this state.");
        }

        return new RuntimeStateCompatibilityDecision(
            RuntimeStateCompatibilityOutcome.CompatibleRestore,
            "Envelope adapter, schema, provider, model, toolset, and context-policy fingerprints all match the current run.");
    }
}

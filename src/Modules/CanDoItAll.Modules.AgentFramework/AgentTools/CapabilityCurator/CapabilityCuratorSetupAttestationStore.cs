using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class CapabilityCuratorSetupAttestationStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, AttestationEntry> attestations =
        new(StringComparer.Ordinal);

    public CapabilityCuratorSetupAttestation Issue(
        string attestationScopeKey,
        CapabilityCuratorSetupKind kind,
        string candidateFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attestationScopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateFingerprint);
        RemoveExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAtUtc = timeProvider.GetUtcNow().Add(Lifetime);
        attestations[token] = new AttestationEntry(
            attestationScopeKey,
            kind,
            candidateFingerprint,
            expiresAtUtc);
        return new CapabilityCuratorSetupAttestation(token, candidateFingerprint, expiresAtUtc);
    }

    public void Consume(
        string attestationScopeKey,
        CapabilityCuratorSetupKind kind,
        string candidateFingerprint,
        string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attestationScopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateFingerprint);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedAccessException(
                "Capability save requires a current, one-time setup attestation for the exact Tool or MCP candidate.");
        }

        if (!attestations.TryRemove(token.Trim(), out var entry) ||
            entry.ExpiresAtUtc <= timeProvider.GetUtcNow() ||
            entry.Kind != kind ||
            !string.Equals(entry.AttestationScopeKey, attestationScopeKey, StringComparison.Ordinal) ||
            !string.Equals(entry.CandidateFingerprint, candidateFingerprint, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Capability save requires a current, one-time setup attestation for the exact Tool or MCP candidate.");
        }
    }

    private void RemoveExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in attestations)
        {
            if (item.Value.ExpiresAtUtc < now)
            {
                attestations.TryRemove(item.Key, out _);
            }
        }
    }

    private sealed record AttestationEntry(
        string AttestationScopeKey,
        CapabilityCuratorSetupKind Kind,
        string CandidateFingerprint,
        DateTimeOffset ExpiresAtUtc);
}

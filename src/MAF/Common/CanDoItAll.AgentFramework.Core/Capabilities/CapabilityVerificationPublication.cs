using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum CapabilityVerificationDisposition {
    Rejected, CanceledBeforeDiagnostic, DiagnosticInterrupted, Superseded, PublicationCanceled, PublicationNotStarted, Committed, Unconfirmed
}

public enum CapabilityProofRecovery { Satisfied, NotPublished, Superseded, Unconfirmed }

public sealed class CapabilityProofReceipt {
    private readonly string beforeAgent;
    private readonly string beforeCapability;
    private readonly string publishedAgent;
    private readonly string publishedCapability;

    internal CapabilityProofReceipt(Guid attemptId, AgentDefinition agent, CapabilityCatalogItem capability, CapabilityVerificationResult proof,
        string providerFingerprint) {
        AttemptId = attemptId;
        AgentId = agent.Id;
        CapabilityId = capability.Id;
        ExpectedUpdatedAtUtc = agent.UpdatedAtUtc;
        CheckedAtUtc = proof.CheckedAtUtc;
        beforeAgent = Fingerprint(agent);
        beforeCapability = Fingerprint(capability);
        publishedAgent = Fingerprint(Apply(agent, capability.Id, proof));
        publishedCapability = Fingerprint(Apply(capability, proof));
        InputFingerprint = Fingerprint(new { beforeAgent, beforeCapability, providerFingerprint });
    }

    public Guid AttemptId { get; }
    public Guid AgentId { get; }
    public Guid CapabilityId { get; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; }
    public DateTimeOffset CheckedAtUtc { get; }
    public string InputFingerprint { get; }

    public CapabilityProofRecovery Classify(IReadOnlyList<AgentDefinition> agents, IReadOnlyList<CapabilityCatalogItem> capabilities) {
        var matches = agents.Where(agent => agent.Id == AgentId).ToArray();
        var definitions = capabilities.Where(capability => capability.Id == CapabilityId).ToArray();
        if (matches.Length > 1 || definitions.Length > 1) {
            return CapabilityProofRecovery.Unconfirmed;
        }
        if (matches.Length == 0 || definitions.Length == 0) {
            return CapabilityProofRecovery.Superseded;
        }
        var agentHash = Fingerprint(matches[0]);
        var capabilityHash = Fingerprint(definitions[0]);
        if (agentHash == publishedAgent && capabilityHash == publishedCapability) {
            return CapabilityProofRecovery.Satisfied;
        }
        if (agentHash == beforeAgent && capabilityHash == beforeCapability) {
            return CapabilityProofRecovery.NotPublished;
        }
        return matches[0].UpdatedAtUtc < ExpectedUpdatedAtUtc
            ? CapabilityProofRecovery.Unconfirmed : CapabilityProofRecovery.Superseded;
    }

    internal bool MatchesInputs(AgentDefinition agent, CapabilityCatalogItem capability)
        => Fingerprint(agent) == beforeAgent && Fingerprint(capability) == beforeCapability;

    internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
    internal static AgentDefinition Apply(AgentDefinition agent, Guid capabilityId, CapabilityVerificationResult proof) => agent with {
        Capabilities = agent.Capabilities.Select(attachment => attachment.CapabilityId == capabilityId ? attachment with {
            ProofStatus = proof.Status, LastVerifiedAtUtc = proof.CheckedAtUtc, ProofNotes = proof.Notes
        } : attachment).ToArray(),
        UpdatedAtUtc = proof.CheckedAtUtc
    };
    internal static CapabilityCatalogItem Apply(CapabilityCatalogItem capability, CapabilityVerificationResult proof) => capability with {
        ProofStatus = proof.Status, LastVerifiedAtUtc = proof.CheckedAtUtc, ProofNotes = proof.Notes
    };
}

public sealed record CapabilityVerificationOutcome(CapabilityVerificationDisposition Disposition, CapabilityProofReceipt? Receipt = null);

public sealed class CapabilityVerificationException(CapabilityVerificationOutcome outcome)
    : InvalidOperationException("Capability verification did not complete publication. Inspect the typed outcome before another diagnostic.") {
    public CapabilityVerificationOutcome Outcome { get; } = outcome;
}

internal sealed class CapabilityVerificationPublication(ISandboxWorkspaceCatalogStore store,
    ICapabilityProofService diagnostics, IProviderRuntimeProfileSnapshotSource providers) {
    public async Task<CapabilityVerificationOutcome> ExecuteAsync(Guid agentId, Guid capabilityId, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return new(CapabilityVerificationDisposition.CanceledBeforeDiagnostic);
        }
        var attemptId = Guid.NewGuid();
        SandboxWorkspaceCatalogSnapshot snapshot;
        AgentDefinition agent;
        CapabilityCatalogItem capability;
        ProviderRuntimeProfileSnapshotLease? provider;
        try {
            snapshot = await store.LoadCatalogSnapshotAsync(token);
            var currentAgent = snapshot.Catalog.Agents.SingleOrDefault(item => item.Id == agentId);
            var currentCapability = snapshot.Catalog.Capabilities.SingleOrDefault(item => item.Id == capabilityId);
            if (currentAgent is null || currentCapability is null || currentAgent.Capabilities.Count(item => item.CapabilityId == capabilityId) != 1) {
                return new(CapabilityVerificationDisposition.Rejected);
            }
            agent = CapabilityProofReceipt.Copy(currentAgent);
            capability = CapabilityProofReceipt.Copy(currentCapability);
            provider = agent.ProviderProfileId is { } providerId ? await providers.AcquireProviderAsync(providerId, snapshot, token) : null;
            if (agent.ProviderProfileId.HasValue && provider is null) {
                return new(CapabilityVerificationDisposition.Rejected);
            }
            provider = CapabilityProofReceipt.Copy(provider);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return new(CapabilityVerificationDisposition.CanceledBeforeDiagnostic);
        } catch (Exception) {
            return new(CapabilityVerificationDisposition.Rejected);
        }

        CapabilityVerificationResult proof;
        try {
            token.ThrowIfCancellationRequested();
            proof = await diagnostics.VerifyAsync(CapabilityProofReceipt.Copy(agent), CapabilityProofReceipt.Copy(provider?.Profile), CapabilityProofReceipt.Copy(capability), token);
        } catch (Exception) {
            return new(CapabilityVerificationDisposition.DiagnosticInterrupted);
        }
        var providerFingerprint = CapabilityProofReceipt.Fingerprint(provider);
        var receipt = new CapabilityProofReceipt(attemptId, agent, capability, proof, providerFingerprint);
        if (token.IsCancellationRequested) {
            return new(CapabilityVerificationDisposition.PublicationCanceled, receipt);
        }
        try {
            var latest = await store.LoadCatalogSnapshotAsync(token);
            var currentProvider = agent.ProviderProfileId is { } providerId ? await providers.AcquireProviderAsync(providerId, latest, token) : null;
            if (CapabilityProofReceipt.Fingerprint(currentProvider) != providerFingerprint) {
                return new(CapabilityVerificationDisposition.Superseded, receipt);
            }
        } catch (Exception) {
            return new(token.IsCancellationRequested ? CapabilityVerificationDisposition.PublicationCanceled
                : CapabilityVerificationDisposition.PublicationNotStarted, receipt);
        }
        try {
            await store.UpdateCatalogAsync(current => {
                var currentAgent = current.Agents.SingleOrDefault(item => item.Id == agentId);
                var currentCapability = current.Capabilities.SingleOrDefault(item => item.Id == capabilityId);
                var currentProvider = agent.ProviderProfileId is { } providerId
                    ? providers.CaptureProvider(providerId, new(current, snapshot.Revision)) : null;
                if (currentAgent is null || currentCapability is null || !receipt.MatchesInputs(currentAgent, currentCapability) ||
                    CapabilityProofReceipt.Fingerprint(currentProvider) != providerFingerprint) {
                    throw new VerificationSupersededException();
                }
                return current with {
                    Agents = current.Agents.Select(item => item.Id == agentId ? CapabilityProofReceipt.Apply(item, capabilityId, proof) : item).ToArray(),
                    Capabilities = current.Capabilities.Select(item => item.Id == capabilityId ? CapabilityProofReceipt.Apply(item, proof) : item).ToArray()
                };
            }, token);
            return new(CapabilityVerificationDisposition.Committed, receipt);
        } catch (VerificationSupersededException) {
            return new(CapabilityVerificationDisposition.Superseded, receipt);
        } catch (Exception) {
            return new(CapabilityVerificationDisposition.Unconfirmed, receipt);
        }
    }

    private sealed class VerificationSupersededException : Exception;
}

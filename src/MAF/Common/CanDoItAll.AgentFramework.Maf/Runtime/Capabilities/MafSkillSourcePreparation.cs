using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafSkillSourcePreparation(IAgentWorkspaceToolResultSource? source,
    AgentRuntimeToolProviderContext context, string workspaceRoot, WorkspaceScopeDescriptor workspaceScope,
    AgentToolSemanticDigest configuration,
    Func<IAgentWorkspaceToolResultReadLease, AgentToolSemanticDigest> currentConfiguration)
    : IMafContextToolSourcePreparation {
    private static readonly AsyncLocal<Observation?> CurrentObservation = new();
    private Snapshot? prepared;
    internal MafSkillResultDisclosure? ResultDisclosure { get; init; }

    internal Observation Begin() {
        var observation = new Observation(this, CurrentObservation.Value);
        CurrentObservation.Value = observation;
        return observation;
    }

    internal bool Observe(Microsoft.Agents.AI.AgentSkill skill) {
        if (CurrentObservation.Value is not { } current || !ReferenceEquals(current.Owner, this) ||
                skill is not MafPreparedSkill owned) {
            throw Missing();
        }
        current.Add(new(skill.Frontmatter.Name, owned.Origin));
        return true;
    }

    public AgentToolProtocolEnvelope Prepare(string toolName, JsonElement arguments) {
        var snapshot = prepared ?? throw Missing();
        var name = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("skillName", out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        return new MafSkillSourceEvidence(snapshot.Source, workspaceRoot, configuration, snapshot.Digest,
            name, snapshot.Candidates.Where(item => item.Name == name).ToImmutableArray()).Write();
    }

    public async ValueTask ValidateAsync(AgentToolProtocolEnvelope envelope, CancellationToken cancellationToken) {
        var original = MafSkillSourceEvidence.Read(envelope);
        if (original.WorkspaceRoot != workspaceRoot || original.Configuration != configuration ||
                prepared is { } attached && (attached.Digest != original.CandidateDigest ||
                    !attached.Candidates.Where(item => item.Name == original.SkillName).SequenceEqual(original.Candidates))) {
            throw Changed();
        }
        await ValidateCurrentAsync(original.Source, original.Configuration, cancellationToken);
    }

    private async ValueTask ValidateCurrentAsync(AgentToolProtocolEnvelope originalSource, AgentToolSemanticDigest originalConfiguration,
        CancellationToken cancellationToken) {
        await using var held = await RequireSource().AcquireReadAsync(context, workspaceScope, originalSource, cancellationToken);
        if (currentConfiguration(held) != originalConfiguration) {
            throw Changed();
        }
        held.RequireCurrent();
    }

    internal async ValueTask RequireInvocationAsync(MafSkillSourceEntry selected, CancellationToken cancellationToken) {
        var claim = AgentToolInvocationClaim.Current ?? throw Missing();
        try {
            var envelope = claim.Proposal.Payload.SourcePreparation ?? throw Missing();
            var original = MafSkillSourceEvidence.Read(envelope);
            using var arguments = JsonDocument.Parse(claim.Proposal.Payload.ArgumentsJson);
            if (envelope != Prepare(claim.Proposal.Payload.ToolName, arguments.RootElement) || !original.Candidates.Contains(selected)) {
                throw Changed();
            }
            await ValidateAsync(envelope, cancellationToken);
        } catch (AgentToolAdmissionException) {
            throw new AgentToolPolicyBlockedException(claim.Proposal.Payload.ToolName,
                ToolInvocationDecisionKind.Deny, "The originally prepared skill source is no longer authorized.");
        }
    }

    private IAgentWorkspaceToolResultSource RequireSource() => source ?? throw Missing();

    private ValueTask<AgentToolProtocolEnvelope> CaptureSourceAsync(CancellationToken cancellationToken)
        => RequireSource().CaptureAsync(context, workspaceScope, cancellationToken);

    private async ValueTask AttachAsync(AgentToolProtocolEnvelope captured, ImmutableArray<MafSkillSourceEntry> candidates,
        CancellationToken cancellationToken) {
        await ValidateCurrentAsync(captured, configuration, cancellationToken);
        var digest = MafToolProtocolCodec.Digest(candidates);
        if (prepared is { } original && (original.Digest != digest || original.Source != captured)) {
            throw Changed();
        }
        prepared = new(captured, digest, candidates);
    }

    internal static AgentToolSemanticDigest ConfigurationDigest(AgentRuntimeConfiguration configuration,
        IReadOnlyList<CapabilityCatalogItem> capabilities, IReadOnlyList<string> roots, AgentWorkspaceToolAccessSettings access) {
        var value = JsonSerializer.SerializeToElement(new {
            PreferredRoots = configuration.PreferredSkillRoots ?? [], Roots = roots.Order(StringComparer.Ordinal).ToArray(),
            access.CanReadFiles, access.CanRunLocalScripts,
            Capabilities = capabilities.Where(item => item.Kind == CapabilityKind.Skill).OrderBy(item => item.Id).Select(item => new {
                item.Id, item.Key, item.Name, item.Description, item.EndpointOrPath,
                Configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(item.ConfigurationJson)
            }).ToArray()
        });
        return AgentToolProtocolEnvelope.ComputeDigest(MafToolProtocolCodec.Canonicalize(value));
    }

    private static AgentToolAdmissionException Missing() => MafContextToolSourceContract.MissingSource();

    internal static AgentToolAdmissionException Changed()
        => new("tool-admission.source-preparation-changed",
            "The originally prepared skill source, configuration or workspace is no longer available unchanged. The saved proposal will not be moved or executed automatically.");

    private sealed record Snapshot(AgentToolProtocolEnvelope Source, AgentToolSemanticDigest Digest,
        ImmutableArray<MafSkillSourceEntry> Candidates);

    internal sealed class Observation(MafSkillSourcePreparation owner, Observation? previous) : IDisposable {
        private readonly List<MafSkillSourceEntry> candidates = [];
        private AgentToolProtocolEnvelope? capturedSource;
        private bool disposed;
        internal MafSkillSourcePreparation Owner => owner;

        internal async ValueTask CaptureAsync(CancellationToken cancellationToken) {
            capturedSource = await owner.CaptureSourceAsync(cancellationToken);
        }

        internal void Add(MafSkillSourceEntry entry) {
            ObjectDisposedException.ThrowIf(disposed, this);
            candidates.Add(entry);
        }

        internal async ValueTask CompleteAsync(CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(disposed, this);
            await owner.AttachAsync(capturedSource ?? throw Missing(), candidates.ToImmutableArray(), cancellationToken);
        }

        public void Dispose() {
            if (!disposed) {
                disposed = true;
                CurrentObservation.Value = previous;
            }
        }
    }
}

internal enum MafSkillOriginKind { File, Inline, Registered }

internal sealed record MafSkillOrigin(MafSkillOriginKind Kind, Guid? CapabilityId, string Root, string Location, string Implementation);
internal sealed record MafSkillSourceEntry(string Name, MafSkillOrigin Origin);

internal sealed record MafSkillSourceEvidence(AgentToolProtocolEnvelope Source, string WorkspaceRoot,
    AgentToolSemanticDigest Configuration, AgentToolSemanticDigest CandidateDigest, string SkillName,
    ImmutableArray<MafSkillSourceEntry> Candidates) {
    internal const int MaximumCandidates = 256;
    private const string Format = "maf-skill-source-preparation";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal AgentToolProtocolEnvelope Write() {
        Validate();
        var value = JsonSerializer.Serialize(this, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(value) > AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes) {
            throw MafContextToolSourceContract.MissingSource();
        }
        return AgentToolProtocolEnvelope.Create(Format, 1, value);
    }

    internal static MafSkillSourceEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != 1) {
            throw MafContextToolSourceContract.MissingSource();
        }
        var value = JsonSerializer.Deserialize<MafSkillSourceEvidence>(envelope.PayloadJson, Json)
            ?? throw MafContextToolSourceContract.MissingSource();
        value.Validate();
        return value;
    }

    private void Validate() {
        if (Source is null || string.IsNullOrWhiteSpace(WorkspaceRoot) || !Path.IsPathFullyQualified(WorkspaceRoot) ||
                string.IsNullOrEmpty(Configuration.Value) || string.IsNullOrEmpty(CandidateDigest.Value) || SkillName is null || SkillName.Length > 512 ||
                Candidates.IsDefault || Candidates.Length > MaximumCandidates ||
                Candidates.Any(item => item is null || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 512 ||
                    item.Name != SkillName || !ValidOrigin(item.Origin))) {
            throw MafContextToolSourceContract.MissingSource();
        }
    }

    private static bool ValidOrigin(MafSkillOrigin? origin) => origin is not null && (origin.Kind switch {
        MafSkillOriginKind.File => origin.CapabilityId is null && Path.IsPathFullyQualified(origin.Root) && Path.IsPathFullyQualified(origin.Location),
        MafSkillOriginKind.Inline => origin.CapabilityId is { } inline && inline != Guid.Empty &&
            origin.Root == string.Empty && origin.Location == string.Empty && origin.Implementation == string.Empty,
        MafSkillOriginKind.Registered => origin.CapabilityId is { } registered && registered != Guid.Empty &&
            !string.IsNullOrWhiteSpace(origin.Implementation) && origin.Root == string.Empty &&
            (origin.Location == string.Empty || Path.IsPathFullyQualified(origin.Location)),
        _ => false
    });
}

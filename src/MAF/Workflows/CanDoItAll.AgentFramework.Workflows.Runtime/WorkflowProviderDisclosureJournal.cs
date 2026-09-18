using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowProviderDisclosureJournal {
    private const string IdentityDomain = "workflow-provider-disclosure-v1";
    private const string PrivateMessage = "Private Workflow provider-disclosure evidence";
    private const int MaximumHeaderBytes = 16_384;
    private const int SimulationChunkBytes = 8_192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int MaximumPartBytes = WorkflowProviderDisclosureProtocol.MaximumEvidencePayloadBytes * 6 + MaximumHeaderBytes;
    private static JsonSerializerOptions JsonOptions => WorkflowProviderDisclosureContent.JsonOptions;

    public sealed record Append(bool VisibleAlreadyRecorded, IReadOnlyList<WorkflowEventRecord> PrivateEvents);

    public static WorkflowEventRecord PublicEvent(WorkflowEventRecord value) => value with {
        DisclosureDeclaration = null, CompletionProof = null, ProviderReadEvidence = []
    };

    public static bool HasDisclosureMetadata(WorkflowEventRecord value) => value.DisclosureDeclaration is not null ||
        value.CompletionProof is not null || value.ProviderReadEvidence.Count != 0;

    public static void RequireOrdinaryEvent(WorkflowEventRecord value) {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.ProviderReadEvidence);
        if (value.Kind == WorkflowEventKind.ProviderReadEvidence || HasDisclosureMetadata(value)) {
            throw Invalid("Private disclosure evidence requires the atomic Workflow event writer.");
        }
    }

    public static Guid LinkId(WorkflowRunId runId, Guid eventId) => Identity(runId, "event", eventId.ToString("N"));

    public static Append Prepare(WorkflowRunSnapshot? run, IReadOnlyList<WorkflowEventRecord> existing, WorkflowEventRecord incoming, bool allowDeclaration = false) {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(incoming.ProviderReadEvidence);
        incoming = incoming with { ProviderReadEvidence = incoming.ProviderReadEvidence.ToArray() };
        if (incoming.Kind == WorkflowEventKind.ProviderReadEvidence) {
            throw Invalid("Private disclosure rows cannot be supplied as public Workflow events.");
        }
        var protectedInput = HasDisclosureMetadata(incoming);
        if (!protectedInput && existing.All(value => value.Id != incoming.Id)) {
            return new(false, []);
        }
        var decoded = Decode(run, incoming.RunId, existing);
        var linked = decoded.Links.SingleOrDefault(link => link.EventId == incoming.Id);
        if (!protectedInput) {
            if (linked is not null) {
                throw Invalid("A retained disclosure event cannot lose its original private evidence.");
            }
            return new(false, []);
        }
        if (run is null || incoming.Id == Guid.Empty || incoming.RunId != run.RunId) {
            throw Invalid("Private evidence requires its persisted original Workflow run and event.");
        }
        if (linked is null && existing.Any(value => value.Id == incoming.Id)) {
            throw Invalid("A previously unproven event cannot acquire disclosure evidence retroactively.");
        }
        var additions = new List<WorkflowEventRecord>();
        Guid? completionId = null;
        if (incoming.DisclosureDeclaration is { } declaration) {
            if (incoming.Kind != WorkflowEventKind.Started || incoming.CompletionProof is not null || incoming.ProviderReadEvidence.Count != 0) {
                throw Invalid("Only the original Started event can declare Workflow disclosure semantics.");
            }
            ValidateDeclaration(run, declaration);
            if (decoded.Declaration is { } retained) {
                if (!Equivalent(retained.Declaration, declaration) || retained.OriginalEventId != incoming.Id) {
                    throw Invalid("The original Workflow disclosure declaration is immutable.");
                }
            } else {
                if (!allowDeclaration || existing.Any(value => value.Kind == WorkflowEventKind.Started)) {
                    throw Invalid("An existing undeclared Workflow cannot acquire a new disclosure declaration.");
                }
                additions.Add(Create(incoming, new DeclarationEntry(declaration with { Simulations = [] }, incoming.Id,
                    SimulationManifest(declaration.Simulations))));
                for (var index = 0; index < declaration.Simulations.Length; index++) {
                    AddSimulationParts(additions, incoming, index, declaration.Simulations[index]);
                }
            }
        } else if (incoming.CompletionProof is { } proof) {
            var originalDeclaration = decoded.Declaration?.Declaration
                ?? throw Invalid("A node completion requires the original Workflow disclosure declaration.");
            ValidateProof(run, originalDeclaration, proof);
            if (incoming.Kind != WorkflowEventKind.ExecutorCompleted || incoming.NodeId != proof.NodeId) {
                throw Invalid("A completion proof must accompany its actual completed node event.");
            }
            if (proof.SimulationHash.HasValue && incoming.ProviderReadEvidence.Count != 0) {
                throw Invalid("A simulated completion cannot supply actual owner-read evidence.");
            }
            WorkflowProviderDisclosureContent.RequireEvidenceMatches(proof, incoming.ProviderReadEvidence);
            var manifest = WorkflowProviderDisclosureContent.Manifest(incoming.ProviderReadEvidence);
            completionId = proof.CompletionId;
            if (decoded.Completions.TryGetValue(proof.CompletionId, out var retained)) {
                if (!Equivalent(retained.Proof, proof) || retained.Manifest != manifest) {
                    throw Invalid("A repeated completion must retain the full original proof and ordered owner evidence.");
                }
            } else {
                additions.Add(Create(incoming, new CompletionEntry(proof, manifest, incoming.Id)));
                for (var index = 0; index < incoming.ProviderReadEvidence.Count; index++) {
                    additions.Add(Create(incoming, new PartEntry(proof.CompletionId, index, incoming.ProviderReadEvidence[index])));
                }
            }
        } else {
            throw Invalid("Owner evidence cannot be saved without its exact completion proof.");
        }
        var nextLink = new LinkEntry(incoming.Id, completionId, PublicHash(incoming));
        if (linked is not null) {
            if (linked != nextLink) {
                throw Invalid("The original visible Workflow event and its private evidence are immutable.");
            }
            return new(true, additions);
        }
        additions.Add(Create(incoming, nextLink));
        var occupied = existing.Select(value => value.Id).ToHashSet();
        if (additions.Any(value => !occupied.Add(value.Id) || value.Id == incoming.Id)) {
            throw Invalid("The private Workflow evidence identity is already occupied.");
        }
        return new(false, additions);
    }

    public static WorkflowProviderDisclosureHistory Read(WorkflowRunSnapshot? run, WorkflowRunId runId,
        IReadOnlyList<WorkflowEventRecord> events) {
        var decoded = Decode(run, runId, events);
        var provenIds = decoded.Links.Where(link => link.CompletionId.HasValue).Select(link => link.EventId).ToHashSet();
        var unproven = events.Where(value => value.Kind == WorkflowEventKind.ExecutorCompleted &&
                value.NodeId.HasValue && !provenIds.Contains(value.Id) && IsOwnerProgress(value))
            .OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).Select(value => value.NodeId!.Value).Distinct().ToArray();
        return new(decoded.Declaration?.Declaration, decoded.Reads, unproven);
    }

    private static Decoded Decode(WorkflowRunSnapshot? run, WorkflowRunId runId, IReadOnlyList<WorkflowEventRecord> events) {
        var privateRows = events.Where(value => value.Kind == WorkflowEventKind.ProviderReadEvidence).ToArray();
        if (privateRows.Length == 0) {
            return new(null, new Dictionary<Guid, CompletionEntry>(), [], []);
        }
        if (run is null || run.RunId != runId || events.Any(value => value.RunId != runId)) {
            throw Invalid("Private Workflow history has no matching original run.");
        }
        if (privateRows.Select(row => row.Id).Distinct().Count() != privateRows.Length) {
            throw Invalid("Private Workflow history contains duplicate row identities.");
        }
        var entries = privateRows.Select(row => Parse(runId, row)).ToArray();
        var declarations = entries.OfType<DeclarationEntry>().ToArray();
        if (declarations.Length != 1) {
            throw Invalid("Private Workflow history requires exactly one original declaration.");
        }
        var declaration = declarations[0];
        if (!declaration.Declaration.Simulations.IsEmpty) {
            throw Invalid("Simulation admission must retain its bounded ordered parts.");
        }
        var admittedSimulations = ReadSimulations(entries, declaration.SimulationManifest);
        if (SimulationManifest(admittedSimulations) != declaration.SimulationManifest) {
            throw Invalid("The original Workflow simulation admission does not match its manifest.");
        }
        declaration = declaration with { Declaration = declaration.Declaration with { Simulations = admittedSimulations } };
        ValidateDeclaration(run, declaration.Declaration);
        var completions = entries.OfType<CompletionEntry>().ToArray();
        if (completions.Select(value => value.Proof.CompletionId).Distinct().Count() != completions.Length) {
            throw Invalid("Private Workflow history contains duplicate completion identities.");
        }
        var byCompletion = completions.ToDictionary(value => value.Proof.CompletionId);
        var parts = entries.OfType<PartEntry>().ToArray();
        if (parts.Any(part => !byCompletion.ContainsKey(part.CompletionId))) {
            throw Invalid("Private Workflow evidence contains an orphan part.");
        }
        var partsByCompletion = parts.ToLookup(part => part.CompletionId);
        var visibleById = events.Where(value => value.Kind != WorkflowEventKind.ProviderReadEvidence).ToLookup(value => value.Id);
        var links = entries.OfType<LinkEntry>().ToArray();
        if (links.Select(link => link.EventId).Distinct().Count() != links.Length) {
            throw Invalid("Private Workflow evidence contains duplicate visible event links.");
        }
        foreach (var link in links) {
            var visible = visibleById[link.EventId].ToArray();
            if (visible.Length != 1 || PublicHash(visible[0]) != link.EventHash) {
                throw Invalid("Private Workflow evidence lost or changed its original visible event.");
            }
            if (link.CompletionId is { } completionId) {
                if (!byCompletion.TryGetValue(completionId, out var completion) || visible[0].Kind != WorkflowEventKind.ExecutorCompleted ||
                        visible[0].NodeId != completion.Proof.NodeId) {
                    throw Invalid("A private completion link does not match its actual node event.");
                }
            } else if (visible[0].Kind != WorkflowEventKind.Started) {
                throw Invalid("The declaration link does not reference its original Started event.");
            }
        }
        if (!links.Any(link => link.EventId == declaration.OriginalEventId && link.CompletionId is null)) {
            throw Invalid("The original Workflow declaration event link is missing.");
        }
        var reads = new List<WorkflowCompletedNodeRead>();
        foreach (var completion in completions) {
            ValidateProof(run, declaration.Declaration, completion.Proof);
            if (!links.Any(link => link.EventId == completion.OriginalEventId && link.CompletionId == completion.Proof.CompletionId)) {
                throw Invalid("The original completed Workflow event link is missing.");
            }
            var ordered = partsByCompletion[completion.Proof.CompletionId].OrderBy(part => part.Index).ToArray();
            if (ordered.Length != (completion.Manifest?.PartCount ?? 0) || ordered.Where((part, index) => part.Index != index).Any()) {
                throw Invalid("The completed Workflow evidence has missing, duplicate or unordered parts.");
            }
            completion.Manifest?.Validate();
            var evidence = ordered.Select(part => part.Evidence).ToArray();
            if (completion.Proof.SimulationHash.HasValue && evidence.Length != 0) {
                throw Invalid("A retained simulated completion cannot contain actual owner-read evidence.");
            }
            WorkflowProviderDisclosureContent.RequireEvidenceMatches(completion.Proof, evidence);
            if (WorkflowProviderDisclosureContent.Manifest(evidence) != completion.Manifest) {
                throw Invalid("The completed Workflow evidence does not match its original manifest.");
            }
            reads.Add(new(completion.Proof, completion.Manifest, evidence));
        }
        return new(declaration, byCompletion, links, reads);
    }

    private static void ValidateDeclaration(WorkflowRunSnapshot run, WorkflowRunDisclosureDeclaration declaration) {
        declaration.Validate();
        if (declaration.Simulations.Any(simulation => simulation.Step is null)) {
            throw Invalid("Private Workflow simulation admission requires every exact original step.");
        }
        if (declaration.RunId != run.RunId || declaration.WorkflowId != run.WorkflowId || declaration.VersionId != run.VersionId ||
                declaration.SourceHash != WorkflowProviderDisclosureContent.Source(run.Origin)) {
            throw Invalid("The Workflow declaration differs from its original run, version or source.");
        }
    }

    private static void ValidateProof(WorkflowRunSnapshot run, WorkflowRunDisclosureDeclaration declaration, WorkflowNodeCompletionProof proof) {
        proof.Validate();
        if (proof.Occurrence.RunId != run.RunId || proof.WorkflowId != run.WorkflowId || proof.VersionId != run.VersionId ||
                proof.DefinitionHash != declaration.DefinitionHash || proof.SourceHash != declaration.SourceHash ||
                proof.CompilerVersion != declaration.CompilerVersion ||
                proof.SimulationHash != declaration.Simulations.SingleOrDefault(value => value.NodeId == proof.NodeId)?.Hash) {
            throw Invalid("The completed node proof differs from its original run declaration.");
        }
    }

    private static WorkflowEventRecord Create(WorkflowEventRecord visible, Entry entry) {
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        RequireBound(json, entry);
        return new(EntryId(visible.RunId, entry), visible.RunId, WorkflowEventKind.ProviderReadEvidence,
            visible.NodeId, PrivateMessage, json, visible.CreatedAtUtc);
    }

    private static Entry Parse(WorkflowRunId runId, WorkflowEventRecord row) {
        if (Encoding.UTF8.GetByteCount(row.PayloadJson) > MaximumPartBytes) {
            throw Invalid("A private Workflow evidence record exceeds its bound.");
        }
        Entry entry;
        try {
            entry = JsonSerializer.Deserialize<Entry>(row.PayloadJson, JsonOptions)
                ?? throw Invalid("A private Workflow evidence record is missing.");
            RequireBound(row.PayloadJson, entry);
            ValidateEntry(entry);
        } catch (Exception failure) when (failure is JsonException or ArgumentException or NotSupportedException) {
            throw Invalid("A private Workflow evidence record has invalid JSON, identity or an unsupported schema.");
        }
        if (row.Id != EntryId(runId, entry) || row.Message != PrivateMessage) {
            throw Invalid("A private Workflow evidence record has a different original identity.");
        }
        return entry;
    }

    private static void ValidateEntry(Entry entry) {
        switch (entry) {
            case DeclarationEntry declaration when declaration.Declaration is not null && declaration.OriginalEventId != Guid.Empty:
                declaration.Declaration.Validate();
                declaration.SimulationManifest?.Validate();
                break;
            case CompletionEntry completion when completion.Proof is not null && completion.OriginalEventId != Guid.Empty:
                completion.Proof.Validate();
                completion.Manifest?.Validate();
                break;
            case PartEntry part when part.CompletionId != Guid.Empty && part.Index >= 0 && part.Evidence is not null:
                part.Evidence.Validate();
                break;
            case SimulationHeaderEntry header when header.Index >= 0 && header.ByteLength > 0 && header.PartCount > 0 &&
                    header.PartCount == ((long)header.ByteLength + SimulationChunkBytes - 1) / SimulationChunkBytes:
                header.Hash.Validate();
                break;
            case SimulationChunkEntry chunk when chunk.Index >= 0 && chunk.PartIndex >= 0 && chunk.ContentBase64 is { Length: > 0 } &&
                    chunk.ContentBase64.Length <= (SimulationChunkBytes + 2) / 3 * 4:
                break;
            case LinkEntry link when link.EventId != Guid.Empty && link.CompletionId != Guid.Empty:
                link.EventHash.Validate();
                break;
            default:
                throw Invalid("A private Workflow evidence record has incomplete identity or content.");
        }
    }

    private static WorkflowReadEvidenceManifest? SimulationManifest(ImmutableArray<WorkflowNodeSimulationAdmission> simulations) =>
        simulations.IsEmpty ? null : new(simulations.Length,
            WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(simulations, JsonOptions)));

    private static void AddSimulationParts(List<WorkflowEventRecord> additions, WorkflowEventRecord incoming,
        int index, WorkflowNodeSimulationAdmission simulation) {
        var json = JsonSerializer.Serialize(simulation, JsonOptions);
        var bytes = StrictUtf8.GetBytes(json);
        var count = (int)(((long)bytes.Length + SimulationChunkBytes - 1) / SimulationChunkBytes);
        additions.Add(Create(incoming, new SimulationHeaderEntry(index, count, bytes.Length, WorkflowExecutionContentHash.Compute(json))));
        for (var partIndex = 0; partIndex < count; partIndex++) {
            var offset = partIndex * SimulationChunkBytes;
            additions.Add(Create(incoming, new SimulationChunkEntry(index, partIndex,
                Convert.ToBase64String(bytes, offset, Math.Min(SimulationChunkBytes, bytes.Length - offset)))));
        }
    }

    private static ImmutableArray<WorkflowNodeSimulationAdmission> ReadSimulations(Entry[] entries, WorkflowReadEvidenceManifest? manifest) {
        var headers = entries.OfType<SimulationHeaderEntry>().OrderBy(header => header.Index).ToArray();
        var chunks = entries.OfType<SimulationChunkEntry>().ToArray();
        var headerIndices = headers.Select(header => header.Index).ToHashSet();
        if (headers.Length != (manifest?.PartCount ?? 0) || headers.Where((header, index) => header.Index != index).Any() ||
                chunks.Any(chunk => !headerIndices.Contains(chunk.Index))) {
            throw Invalid("The original Workflow simulation admission has missing, duplicate or orphan parts.");
        }
        var simulations = ImmutableArray.CreateBuilder<WorkflowNodeSimulationAdmission>(headers.Length);
        var chunksByIndex = chunks.ToLookup(chunk => chunk.Index);
        foreach (var header in headers) {
            var ordered = chunksByIndex[header.Index].OrderBy(chunk => chunk.PartIndex).ToArray();
            if (ordered.Length != header.PartCount || ordered.Where((chunk, index) => chunk.PartIndex != index).Any()) {
                throw Invalid("The retained simulation has missing, duplicate or unordered chunks.");
            }
            var bytes = new byte[header.ByteLength];
            for (var index = 0; index < ordered.Length; index++) {
                byte[] chunk;
                try {
                    chunk = Convert.FromBase64String(ordered[index].ContentBase64);
                } catch (FormatException) {
                    throw Invalid("A retained simulation chunk has invalid encoding.");
                }
                var offset = index * SimulationChunkBytes;
                if (chunk.Length != Math.Min(SimulationChunkBytes, bytes.Length - offset) ||
                        Convert.ToBase64String(chunk) != ordered[index].ContentBase64) {
                    throw Invalid("A retained simulation chunk has a different length or encoding.");
                }
                chunk.CopyTo(bytes, offset);
            }
            string json;
            try {
                json = StrictUtf8.GetString(bytes);
            } catch (DecoderFallbackException) {
                throw Invalid("A retained simulation has invalid UTF-8 content.");
            }
            if (WorkflowExecutionContentHash.Compute(json) != header.Hash) {
                throw Invalid("The retained simulation differs from its original content hash.");
            }
            WorkflowNodeSimulationAdmission simulation;
            try {
                simulation = JsonSerializer.Deserialize<WorkflowNodeSimulationAdmission>(json, JsonOptions)
                    ?? throw Invalid("The retained simulation is missing.");
                simulation.Validate();
            } catch (Exception failure) when (failure is JsonException or ArgumentException or NotSupportedException) {
                throw Invalid("The retained simulation content or schema is invalid.");
            }
            if (simulation.Step is null || JsonSerializer.Serialize(simulation, JsonOptions) != json) {
                throw Invalid("The chunked simulation is not the exact retained admission.");
            }
            simulations.Add(simulation);
        }
        return simulations.MoveToImmutable();
    }

    private static bool Equivalent<T>(T first, T second) =>
        string.Equals(JsonSerializer.Serialize(first, JsonOptions), JsonSerializer.Serialize(second, JsonOptions), StringComparison.Ordinal);

    private static void RequireBound(string json, Entry entry) {
        if (Encoding.UTF8.GetByteCount(json) > (entry is PartEntry ? MaximumPartBytes : MaximumHeaderBytes)) {
            throw Invalid("A private Workflow evidence header or part exceeds its bound.");
        }
    }

    private static Guid EntryId(WorkflowRunId runId, Entry entry) => entry switch {
        DeclarationEntry => Identity(runId, "declaration"),
        CompletionEntry completion => Identity(runId, "completion", completion.Proof.CompletionId.ToString("N")),
        PartEntry part => Identity(runId, "part", part.CompletionId.ToString("N"), part.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        LinkEntry link => LinkId(runId, link.EventId),
        SimulationHeaderEntry simulation => Identity(runId, "simulation", simulation.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        SimulationChunkEntry chunk => Identity(runId, "simulation-chunk", chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            chunk.PartIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        _ => throw Invalid("The private Workflow evidence schema is unsupported.")
    };

    private static Guid Identity(WorkflowRunId runId, params string[] parts) {
        var value = JsonSerializer.Serialize(new[] { IdentityDomain, runId.Value.ToString("N") }.Concat(parts), JsonOptions);
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    }

    private static WorkflowExecutionContentHash PublicHash(WorkflowEventRecord value) {
        var ticks = value.CreatedAtUtc.UtcTicks;
        var normalized = PublicEvent(value) with { CreatedAtUtc = new(ticks - ticks % 10, TimeSpan.Zero) };
        return WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(normalized, JsonOptions));
    }

    private static bool IsOwnerProgress(WorkflowEventRecord value) {
        try {
            return JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(value.PayloadJson, JsonOptions)?.Source == WorkflowEventPayloadSource.CanDoItAllProgress;
        } catch (JsonException) {
            return false;
        }
    }

    private static InvalidOperationException Invalid(string message) => new(message);

    private sealed record Decoded(DeclarationEntry? Declaration, IReadOnlyDictionary<Guid, CompletionEntry> Completions,
        IReadOnlyList<LinkEntry> Links, IReadOnlyList<WorkflowCompletedNodeRead> Reads);

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$disclosure")]
    [JsonDerivedType(typeof(DeclarationEntry), "declaration-v1")]
    [JsonDerivedType(typeof(CompletionEntry), "completion-v1")]
    [JsonDerivedType(typeof(PartEntry), "part-v1")]
    [JsonDerivedType(typeof(LinkEntry), "event-link-v1")]
    [JsonDerivedType(typeof(SimulationHeaderEntry), "simulation-header-v2")]
    [JsonDerivedType(typeof(SimulationChunkEntry), "simulation-chunk-v2")]
    private abstract record Entry;

    private sealed record DeclarationEntry(WorkflowRunDisclosureDeclaration Declaration, Guid OriginalEventId,
        WorkflowReadEvidenceManifest? SimulationManifest) : Entry;
    private sealed record CompletionEntry(WorkflowNodeCompletionProof Proof, WorkflowReadEvidenceManifest? Manifest, Guid OriginalEventId) : Entry;
    private sealed record PartEntry(Guid CompletionId, int Index, WorkflowProviderReadEvidence Evidence) : Entry;
    private sealed record SimulationHeaderEntry(int Index, int PartCount, int ByteLength, WorkflowExecutionContentHash Hash) : Entry;
    private sealed record SimulationChunkEntry(int Index, int PartIndex, string ContentBase64) : Entry;
    private sealed record LinkEntry(Guid EventId, Guid? CompletionId, WorkflowExecutionContentHash EventHash) : Entry;
}

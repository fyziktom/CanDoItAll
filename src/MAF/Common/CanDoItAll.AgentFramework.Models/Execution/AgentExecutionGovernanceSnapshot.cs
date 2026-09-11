using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Immutable, provider-neutral, SDK-free permission snapshot for one admitted
/// execution run. It is derived from the canonical
/// <see cref="AgentExecutionAuthorityRecord"/> at admission and is the single
/// runtime enforcement input for capability composition and tool invocation
/// policy. Consumers may narrow further (domain invariants, process
/// restrictions) but must never widen beyond this snapshot. Empty allow-lists
/// mean "not restricted by this dimension", never "everything denied" —
/// read/mutation booleans stay the primary gates.
/// </summary>
public sealed record AgentExecutionGovernanceSnapshot
{
    public AgentExecutionGovernanceSnapshot(
        AgentExecutionAuthorityId authorityId,
        Guid agentId,
        Guid databaseProfileId,
        DatabaseProfileGeneration databaseProfileGeneration,
        WorkspaceScopeDescriptor workspaceScope,
        bool readAllowed,
        bool mutationAllowed,
        string policyVersion,
        string policyFingerprint,
        IReadOnlyList<string>? allowedOperations = null,
        IReadOnlyList<string>? allowedCapabilityKeys = null,
        IReadOnlyList<string>? writableExternalTargetAliases = null,
        IReadOnlyList<string>? readOnlyExternalTargetAliases = null,
        IReadOnlyList<string>? allowedManagedArtifactReadRefs = null)
    {
        if (authorityId.IsEmpty)
        {
            throw new ArgumentException("An execution authority id is required.", nameof(authorityId));
        }

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        ArgumentNullException.ThrowIfNull(workspaceScope);
        if (mutationAllowed && !readAllowed)
        {
            throw new ArgumentException(
                "Mutation authority implies read authority; a mutation-only governance snapshot is invalid.",
                nameof(mutationAllowed));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyFingerprint);

        AuthorityId = authorityId;
        AgentId = agentId;
        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
        WorkspaceScope = workspaceScope;
        ReadAllowed = readAllowed;
        MutationAllowed = mutationAllowed;
        PolicyVersion = policyVersion.Trim();
        PolicyFingerprint = policyFingerprint.Trim();
        AllowedOperations = NormalizeSet(allowedOperations, StringComparer.OrdinalIgnoreCase);
        AllowedCapabilityKeys = NormalizeSet(allowedCapabilityKeys, StringComparer.OrdinalIgnoreCase);
        WritableExternalTargetAliases = NormalizeSet(writableExternalTargetAliases, ExternalTargetAliasCodec.EqualityComparer);
        ReadOnlyExternalTargetAliases = NormalizeSet(readOnlyExternalTargetAliases, ExternalTargetAliasCodec.EqualityComparer);
        AllowedManagedArtifactReadRefs = NormalizeSet(allowedManagedArtifactReadRefs, StringComparer.OrdinalIgnoreCase);
    }

    [JsonConstructor]
    private AgentExecutionGovernanceSnapshot(AgentExecutionAuthorityId authorityId, Guid agentId, Guid databaseProfileId,
        DatabaseProfileGeneration databaseProfileGeneration, WorkspaceScopeDescriptor workspaceScope, bool readAllowed,
        bool mutationAllowed, string policyVersion, string policyFingerprint, ImmutableHashSet<string>? allowedOperations,
        ImmutableHashSet<string>? allowedCapabilityKeys, ImmutableHashSet<string>? writableExternalTargetAliases,
        ImmutableHashSet<string>? readOnlyExternalTargetAliases, ImmutableHashSet<string>? allowedManagedArtifactReadRefs)
        : this(authorityId, agentId, databaseProfileId, databaseProfileGeneration, workspaceScope, readAllowed, mutationAllowed,
            policyVersion, policyFingerprint, (IReadOnlyList<string>?)allowedOperations?.ToArray(), allowedCapabilityKeys?.ToArray(),
            writableExternalTargetAliases?.ToArray(), readOnlyExternalTargetAliases?.ToArray(), allowedManagedArtifactReadRefs?.ToArray()) {
    }

    public AgentExecutionAuthorityId AuthorityId { get; }

    public Guid AgentId { get; }

    public Guid DatabaseProfileId { get; }

    public DatabaseProfileGeneration DatabaseProfileGeneration { get; }

    public WorkspaceScopeDescriptor WorkspaceScope { get; }

    public bool ReadAllowed { get; }

    public bool MutationAllowed { get; }

    public string PolicyVersion { get; }

    public string PolicyFingerprint { get; }

    /// <summary>Empty means "not operation-restricted by the admitted authority".</summary>
    [JsonConverter(typeof(GrantSetJsonConverter))]
    public ImmutableHashSet<string> AllowedOperations { get; }

    /// <summary>Empty means "not capability-restricted by the admitted authority".</summary>
    [JsonConverter(typeof(GrantSetJsonConverter))]
    public ImmutableHashSet<string> AllowedCapabilityKeys { get; }

    [JsonConverter(typeof(GrantSetJsonConverter))]
    public ImmutableHashSet<string> WritableExternalTargetAliases { get; }

    [JsonConverter(typeof(GrantSetJsonConverter))]
    public ImmutableHashSet<string> ReadOnlyExternalTargetAliases { get; }

    [JsonConverter(typeof(GrantSetJsonConverter))]
    public ImmutableHashSet<string> AllowedManagedArtifactReadRefs { get; }

    /// <summary>
    /// Derives the enforcement snapshot from the canonical authority record
    /// produced at turn admission. This is the only production construction
    /// path for context-admitted turns; nothing downstream may re-derive the
    /// grants from UI access entries or agent configuration.
    /// </summary>
    public static AgentExecutionGovernanceSnapshot FromAuthority(AgentExecutionAuthorityRecord authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return new AgentExecutionGovernanceSnapshot(
            authority.AuthorityId,
            authority.AgentId,
            authority.DatabaseProfileId,
            authority.DatabaseProfileGeneration,
            authority.WorkspaceScope,
            authority.ReadAllowed,
            authority.MutationAllowed,
            authority.PolicyVersion,
            authority.PolicyFingerprint,
            authority.AllowedOperations,
            authority.AllowedCapabilityKeys,
            authority.AllowedExternalTargetAliases,
            authority.ReadOnlyExternalTargetAliases);
    }

    private sealed class GrantSetJsonConverter : JsonConverter<ImmutableHashSet<string>> {
        public GrantSetJsonConverter() {
        }

        public override ImmutableHashSet<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var entries = JsonSerializer.Deserialize<string[]>(ref reader, options)
                ?? throw new JsonException("An Agent governance grant set must be an array.");
            return entries.ToImmutableHashSet(StringComparer.Ordinal);
        }

        public override void Write(Utf8JsonWriter writer, ImmutableHashSet<string> value, JsonSerializerOptions options) {
            writer.WriteStartArray();
            foreach (var entry in value.Order(StringComparer.Ordinal)) {
                writer.WriteStringValue(entry);
            }
            writer.WriteEndArray();
        }
    }

    private static ImmutableHashSet<string> NormalizeSet(
        IReadOnlyList<string>? entries,
        IEqualityComparer<string> comparer)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var builder = ImmutableHashSet.CreateBuilder(comparer);
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                builder.Add(entry.Trim());
            }
        }

        return builder.ToImmutable();
    }
}

using System.Collections.Immutable;

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
        AllowedOperations = NormalizeSet(allowedOperations);
        AllowedCapabilityKeys = NormalizeSet(allowedCapabilityKeys);
        WritableExternalTargetAliases = NormalizeSet(writableExternalTargetAliases);
        ReadOnlyExternalTargetAliases = NormalizeSet(readOnlyExternalTargetAliases);
        AllowedManagedArtifactReadRefs = NormalizeSet(allowedManagedArtifactReadRefs);
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
    public ImmutableHashSet<string> AllowedOperations { get; }

    /// <summary>Empty means "not capability-restricted by the admitted authority".</summary>
    public ImmutableHashSet<string> AllowedCapabilityKeys { get; }

    public ImmutableHashSet<string> WritableExternalTargetAliases { get; }

    public ImmutableHashSet<string> ReadOnlyExternalTargetAliases { get; }

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

    private static ImmutableHashSet<string> NormalizeSet(IReadOnlyList<string>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
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

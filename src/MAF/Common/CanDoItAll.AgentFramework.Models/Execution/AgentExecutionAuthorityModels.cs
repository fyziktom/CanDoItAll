using System.Collections.Immutable;

using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifies one resolved execution authority snapshot.
/// </summary>
public readonly record struct AgentExecutionAuthorityId
{
    [JsonConstructor]
    public AgentExecutionAuthorityId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An execution authority id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentExecutionAuthorityId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

/// <summary>
/// Durable, serialization-safe record of what one admitted turn may do. It is
/// produced by the canonical authority resolver from product authorization
/// services — never from UI observation, payload text, or model output. It
/// stores identities, scope, allowed-operation identifiers, generation, and
/// fingerprints only; it never serializes service handles, secret grants, or
/// authorization implementation objects. UI access hints may pre-filter, but
/// this record is the only authority input to workspace service construction.
/// Invariants: mutation authority implies read authority; a project switch
/// always produces a new snapshot; a payload project id can never select one.
/// </summary>
public sealed record AgentExecutionAuthorityRecord
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumAllowedEntryCount = 500;
    public const int MaximumEntryLength = 200;

    public AgentExecutionAuthorityRecord(
        AgentExecutionAuthorityId authorityId,
        Guid agentId,
        Guid databaseProfileId,
        DatabaseProfileGeneration databaseProfileGeneration,
        WorkspaceScopeDescriptor workspaceScope,
        bool readAllowed,
        bool mutationAllowed,
        string policyVersion,
        string policyFingerprint,
        DateTimeOffset resolvedAtUtc,
        string principalId = "",
        IReadOnlyList<string>? allowedOperations = null,
        IReadOnlyList<string>? allowedCapabilityKeys = null,
        IReadOnlyList<string>? allowedExternalTargetAliases = null,
        IReadOnlyList<string>? readOnlyExternalTargetAliases = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (authorityId.IsEmpty)
        {
            throw new ArgumentException("An execution authority id is required.", nameof(authorityId));
        }

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        if (databaseProfileId == Guid.Empty)
        {
            throw new ArgumentException("A database profile id is required.", nameof(databaseProfileId));
        }

        ArgumentNullException.ThrowIfNull(workspaceScope);
        if (mutationAllowed && !readAllowed)
        {
            throw new ArgumentException(
                "Mutation authority implies read authority; a mutation-only authority is invalid.",
                nameof(mutationAllowed));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyFingerprint);
        if (policyFingerprint.Trim().Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A policy fingerprint cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                nameof(policyFingerprint));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "An authority schema version must be positive.");
        }

        AuthorityId = authorityId;
        AgentId = agentId;
        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
        WorkspaceScope = workspaceScope;
        ReadAllowed = readAllowed;
        MutationAllowed = mutationAllowed;
        PolicyVersion = policyVersion.Trim();
        PolicyFingerprint = policyFingerprint.Trim();
        ResolvedAtUtc = resolvedAtUtc;
        PrincipalId = principalId?.Trim() ?? string.Empty;
        AllowedOperations = NormalizeEntries(allowedOperations, nameof(allowedOperations));
        AllowedCapabilityKeys = NormalizeEntries(allowedCapabilityKeys, nameof(allowedCapabilityKeys));
        AllowedExternalTargetAliases = NormalizeEntries(allowedExternalTargetAliases, nameof(allowedExternalTargetAliases));
        ReadOnlyExternalTargetAliases = NormalizeEntries(readOnlyExternalTargetAliases, nameof(readOnlyExternalTargetAliases));
        SchemaVersion = schemaVersion;
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

    public DateTimeOffset ResolvedAtUtc { get; }

    public string PrincipalId { get; }

    public IReadOnlyList<string> AllowedOperations { get; }

    public IReadOnlyList<string> AllowedCapabilityKeys { get; }

    public IReadOnlyList<string> AllowedExternalTargetAliases { get; }

    public IReadOnlyList<string> ReadOnlyExternalTargetAliases { get; }

    public int SchemaVersion { get; }

    private static ImmutableArray<string> NormalizeEntries(
        IReadOnlyList<string>? entries,
        string parameterName)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        if (entries.Count > MaximumAllowedEntryCount)
        {
            throw new ArgumentException(
                $"An authority record cannot carry more than {MaximumAllowedEntryCount} entries.",
                parameterName);
        }

        var builder = ImmutableArray.CreateBuilder<string>(entries.Count);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException("Authority entries cannot be blank.", parameterName);
            }

            var normalized = entry.Trim();
            if (normalized.Length > MaximumEntryLength)
            {
                throw new ArgumentException(
                    $"An authority entry cannot exceed {MaximumEntryLength} characters.",
                    parameterName);
            }

            builder.Add(normalized);
        }

        return builder.MoveToImmutable();
    }
}

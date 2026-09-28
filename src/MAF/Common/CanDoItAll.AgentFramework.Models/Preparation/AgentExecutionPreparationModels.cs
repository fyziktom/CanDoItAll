using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Generation of the active database profile within the running host, as an object whose <c>value</c> is a
/// non-negative integer. It starts at 0 and increases each time the host switches its active database profile.
/// </summary>
public readonly record struct DatabaseProfileGeneration
{
    [JsonConstructor]
    public DatabaseProfileGeneration(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Database profile generation cannot be negative.");
        }

        Value = value;
    }

    /// <summary>The generation number; never negative.</summary>
    public long Value { get; }
}

public readonly record struct ProviderConfigurationFingerprint
{
    public ProviderConfigurationFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

public sealed record AgentExecutionPreparationKey
{
    public AgentExecutionPreparationKey(
        Guid databaseProfileId,
        WorkspaceScopeDescriptor workspaceScope,
        Guid agentId)
    {
        ArgumentNullException.ThrowIfNull(workspaceScope);
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Agent identifier cannot be empty.",
                nameof(agentId));
        }

        DatabaseProfileId = databaseProfileId;
        WorkspaceScope = workspaceScope;
        AgentId = agentId;
    }

    public Guid DatabaseProfileId { get; }

    public WorkspaceScopeDescriptor WorkspaceScope { get; }

    public Guid AgentId { get; }
}

public sealed record AgentExecutionPreparationVersion(
    CatalogDataRevision CatalogRevision,
    DatabaseProfileGeneration DatabaseProfileGeneration,
    ProviderConfigurationFingerprint ProviderFingerprint);

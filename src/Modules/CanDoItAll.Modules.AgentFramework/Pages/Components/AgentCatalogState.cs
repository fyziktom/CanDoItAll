using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentCatalogSnapshot(
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<AgentTeamDefinition> Teams,
    IReadOnlyDictionary<Guid, bool> PrivateProviderById) {
    public static AgentCatalogSnapshot Empty { get; } = new([], [], new Dictionary<Guid, bool>());

    public bool UsesPrivateProvider(AgentDefinition agent)
        => agent.ProviderProfileId is { } id && PrivateProviderById.TryGetValue(id, out var isPrivate) && isPrivate;
}

public sealed record AgentCatalogSelection(Guid? AgentId, Guid? TeamId);

public abstract record AgentCatalogIntent {
    private AgentCatalogIntent() { }

    public sealed record SelectAgent(Guid AgentId) : AgentCatalogIntent;
    public sealed record SelectTeamMember(Guid AgentId) : AgentCatalogIntent;
    public sealed record SelectTeam(Guid? TeamId) : AgentCatalogIntent;
    public sealed record OpenAgent(Guid? AgentId) : AgentCatalogIntent;
    public sealed record OpenTeam(Guid? TeamId) : AgentCatalogIntent;
    public sealed record EditMembers(Guid TeamId) : AgentCatalogIntent;
    public sealed record DeleteTeam(Guid TeamId) : AgentCatalogIntent;
    public sealed record OpenChat(Guid AgentId) : AgentCatalogIntent;
}

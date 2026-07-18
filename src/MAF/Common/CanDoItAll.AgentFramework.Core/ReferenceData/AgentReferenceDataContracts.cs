using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

[Flags]
public enum AgentReferenceDataSections
{
    None = 0,
    Agents = 1,
    Providers = 2
}

public sealed record AgentReferenceDataRequest(
    AgentReferenceDataSections Sections,
    bool IncludeAgentTemplates = false,
    bool ActiveAgentsOnly = false,
    bool EnabledProvidersOnly = false,
    ProviderProfilePurpose? ProviderPurpose = null)
{
    private const AgentReferenceDataSections KnownSections =
        AgentReferenceDataSections.Agents |
        AgentReferenceDataSections.Providers;

    public static AgentReferenceDataRequest AgentsAndProviders(
        bool includeAgentTemplates = false,
        bool activeAgentsOnly = false,
        bool enabledProvidersOnly = false,
        ProviderProfilePurpose? providerPurpose = null)
    {
        return new AgentReferenceDataRequest(
            AgentReferenceDataSections.Agents | AgentReferenceDataSections.Providers,
            includeAgentTemplates,
            activeAgentsOnly,
            enabledProvidersOnly,
            providerPurpose);
    }

    public AgentReferenceDataRequest Normalize()
    {
        var sections = Sections & KnownSections;
        var includesAgents = sections.HasFlag(AgentReferenceDataSections.Agents);
        var includesProviders = sections.HasFlag(AgentReferenceDataSections.Providers);

        return this with
        {
            Sections = sections,
            IncludeAgentTemplates = includesAgents && IncludeAgentTemplates,
            ActiveAgentsOnly = includesAgents && ActiveAgentsOnly,
            EnabledProvidersOnly = includesProviders && EnabledProvidersOnly,
            ProviderPurpose = includesProviders ? ProviderPurpose : null
        };
    }
}

public sealed record AgentReferenceDataSnapshot(
    AgentReferenceDataSections LoadedSections,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<ProviderProfile> Providers,
    IReadOnlyDictionary<Guid, ProviderProfile> ProviderById,
    DateTimeOffset LoadedAtUtc,
    TimeSpan LoadDuration);

public interface IAgentReferenceDataProvider
{
    Task<AgentReferenceDataSnapshot> GetAsync(
        AgentReferenceDataRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentReferenceDataCacheInvalidator
{
    event EventHandler? Invalidated;

    void Invalidate();
}

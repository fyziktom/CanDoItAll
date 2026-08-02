using System.Collections.ObjectModel;
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
    TimeSpan LoadDuration)
{
    public IReadOnlyList<AgentDefinition> Agents { get; } =
        CopyAgents(Agents);

    public IReadOnlyList<ProviderProfile> Providers { get; } =
        CopyProviders(Providers);

    public IReadOnlyDictionary<Guid, ProviderProfile> ProviderById { get; } =
        CopyProviderIndex(ProviderById);

    private static IReadOnlyList<AgentDefinition> CopyAgents(
        IReadOnlyList<AgentDefinition> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        return CopyList(agents.Select(CopyAgent));
    }

    private static AgentDefinition CopyAgent(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var permissions = agent.Permissions ??
            throw new ArgumentException(
                "An agent snapshot cannot contain null permissions.",
                nameof(agent));
        return agent with
        {
            Permissions = permissions with
            {
                AllowedSecrets = CopyList(
                    permissions.NormalizedAllowedSecrets)
            },
            Capabilities = CopyList(agent.Capabilities),
            Tags = CopyList(agent.Tags)
        };
    }

    private static IReadOnlyList<ProviderProfile> CopyProviders(
        IReadOnlyList<ProviderProfile> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return CopyList(providers.Select(CopyProvider));
    }

    private static ProviderProfile CopyProvider(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider with
        {
            SuggestedModels = CopyList(provider.SuggestedModels),
            ModelPrices = CopyList(provider.ModelPrices),
            Tags = CopyList(provider.Tags),
            ModelThinkingEffortCapabilities = CopyList(
                provider.ModelThinkingEffortCapabilities.Select(item => item with
                {
                    AllowedEfforts = CopyList(item.AllowedEfforts)
                }))
        };
    }

    private static IReadOnlyDictionary<Guid, ProviderProfile> CopyProviderIndex(
        IReadOnlyDictionary<Guid, ProviderProfile> providerById)
    {
        ArgumentNullException.ThrowIfNull(providerById);

        var copy = providerById.ToDictionary(
            pair => pair.Key,
            pair => CopyProvider(pair.Value));
        return new ReadOnlyDictionary<Guid, ProviderProfile>(copy);
    }

    private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

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

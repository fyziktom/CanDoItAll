using System.Diagnostics;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceBackedAgentReferenceDataProvider(
    IAgentFrameworkWorkspaceService workspaceService,
    AgentReferenceDataCache cache) : IAgentReferenceDataProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);

    public Task<AgentReferenceDataSnapshot> GetAsync(
        AgentReferenceDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request.Normalize();
        var now = DateTimeOffset.UtcNow;
        if (normalizedRequest.Sections == AgentReferenceDataSections.None)
        {
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.None,
                [],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                now,
                TimeSpan.Zero));
        }

        return cache.GetOrCreateAsync(
            normalizedRequest,
            now,
            CacheTtl,
            () => LoadAsync(normalizedRequest, cancellationToken));
    }

    private async Task<AgentReferenceDataSnapshot> LoadAsync(
        AgentReferenceDataRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var agentsTask = request.Sections.HasFlag(AgentReferenceDataSections.Agents)
            ? workspaceService.ListAgentsAsync(request.IncludeAgentTemplates, cancellationToken)
            : null;
        var providersTask = request.Sections.HasFlag(AgentReferenceDataSections.Providers)
            ? workspaceService.ListProvidersAsync(cancellationToken)
            : null;

        IReadOnlyList<AgentDefinition> agents = [];
        IReadOnlyList<ProviderProfile> providers = [];
        if (agentsTask is not null)
        {
            agents = await agentsTask.ConfigureAwait(false);
            agents = FilterAgents(agents, request);
        }

        if (providersTask is not null)
        {
            providers = await providersTask.ConfigureAwait(false);
            providers = FilterProviders(providers, request);
        }

        stopwatch.Stop();
        return new AgentReferenceDataSnapshot(
            request.Sections,
            agents,
            providers,
            providers.ToDictionary(provider => provider.Id),
            DateTimeOffset.UtcNow,
            stopwatch.Elapsed);
    }

    private static IReadOnlyList<AgentDefinition> FilterAgents(
        IReadOnlyList<AgentDefinition> agents,
        AgentReferenceDataRequest request)
    {
        var query = agents.AsEnumerable();
        if (!request.IncludeAgentTemplates)
        {
            query = query.Where(agent => !agent.IsTemplate);
        }

        if (request.ActiveAgentsOnly)
        {
            query = query.Where(agent => agent.Status == AgentLifecycleStatus.Active);
        }

        return query
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ProviderProfile> FilterProviders(
        IReadOnlyList<ProviderProfile> providers,
        AgentReferenceDataRequest request)
    {
        var query = providers.AsEnumerable();
        if (request.EnabledProvidersOnly)
        {
            query = query.Where(provider => provider.IsEnabled);
        }

        if (request.ProviderPurpose.HasValue)
        {
            query = query.Where(provider => provider.Purpose == request.ProviderPurpose.Value);
        }

        return query
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

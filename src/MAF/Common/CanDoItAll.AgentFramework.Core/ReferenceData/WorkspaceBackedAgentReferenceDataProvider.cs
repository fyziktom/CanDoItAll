using System.Diagnostics;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceBackedAgentReferenceDataProvider(
    IAgentFrameworkWorkspaceService workspaceService,
    AgentReferenceDataCache cache) : IAgentReferenceDataProvider, IDisposable
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);
    private readonly object lifecycleGate = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly SemaphoreSlim workspaceReadGate = new(1, 1);
    private int activeLoadCount;
    private bool disposed;
    private bool lifetimeCancellationCompleted;
    private bool resourcesDisposed;

    public Task<AgentReferenceDataSnapshot> GetAsync(
        AgentReferenceDataRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

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
            factoryCancellationToken => LoadAsync(
                normalizedRequest,
                factoryCancellationToken),
            cancellationToken);
    }

    public void Dispose()
    {
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        try
        {
            lifetimeCancellation.Cancel();
        }
        finally
        {
            CompleteLifetimeCancellation();
        }
    }

    private async Task<AgentReferenceDataSnapshot> LoadAsync(
        AgentReferenceDataRequest request,
        CancellationToken cancellationToken)
    {
        BeginLoad();
        try
        {
            using var loadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    lifetimeCancellation.Token);
            var loadCancellationToken = loadCancellation.Token;
            var stopwatch = Stopwatch.StartNew();
            await workspaceReadGate
                .WaitAsync(loadCancellationToken)
                .ConfigureAwait(false);
            try
            {
                IReadOnlyList<AgentDefinition> agents = [];
                if (request.Sections.HasFlag(AgentReferenceDataSections.Agents))
                {
                    agents = await workspaceService
                        .ListAgentsAsync(
                            request.IncludeAgentTemplates,
                            loadCancellationToken)
                        .ConfigureAwait(false);
                    agents = FilterAgents(agents, request);
                }

                IReadOnlyList<ProviderProfile> providers = [];
                if (request.Sections.HasFlag(AgentReferenceDataSections.Providers))
                {
                    providers = await workspaceService
                        .ListProvidersAsync(loadCancellationToken)
                        .ConfigureAwait(false);
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
            finally
            {
                workspaceReadGate.Release();
            }
        }
        finally
        {
            EndLoad();
        }
    }

    private void BeginLoad()
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeLoadCount++;
        }
    }

    private void EndLoad()
    {
        var disposeResources = false;
        lock (lifecycleGate)
        {
            activeLoadCount--;
            disposeResources = TryMarkResourcesForDisposal();
        }

        if (disposeResources)
        {
            DisposeResources();
        }
    }

    private void CompleteLifetimeCancellation()
    {
        var disposeResources = false;
        lock (lifecycleGate)
        {
            lifetimeCancellationCompleted = true;
            disposeResources = TryMarkResourcesForDisposal();
        }

        if (disposeResources)
        {
            DisposeResources();
        }
    }

    private bool TryMarkResourcesForDisposal()
    {
        if (!disposed ||
            !lifetimeCancellationCompleted ||
            activeLoadCount != 0 ||
            resourcesDisposed)
        {
            return false;
        }

        resourcesDisposed = true;
        return true;
    }

    private void DisposeResources()
    {
        try
        {
            workspaceReadGate.Dispose();
        }
        finally
        {
            lifetimeCancellation.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
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

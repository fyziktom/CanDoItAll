using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatPreparationPool : IAgentChatPreparationPool, IDisposable
{
    private static readonly TimeSpan MaximumCatalogValidationAge = TimeSpan.FromSeconds(20);
    private readonly object gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly Dictionary<Guid, PreparedEntry> preparedEntries = [];
    private readonly Dictionary<Guid, long> usageCounts = [];
    private readonly IAgentReferenceDataProvider referenceDataProvider;
    private readonly IAgentReferenceDataCacheInvalidator? referenceDataCacheInvalidator;
    private readonly TimeProvider timeProvider;
    private FloatingAgentChatSettings settings = FloatingAgentChatSettings.Default;
    private long cacheHits;
    private long cacheMisses;
    private long invalidationVersion;

    public AgentChatPreparationPool(
        IAgentReferenceDataProvider referenceDataProvider,
        TimeProvider timeProvider,
        IAgentReferenceDataCacheInvalidator? referenceDataCacheInvalidator = null)
    {
        this.referenceDataProvider = referenceDataProvider;
        this.timeProvider = timeProvider;
        this.referenceDataCacheInvalidator = referenceDataCacheInvalidator;
        if (referenceDataCacheInvalidator is not null)
        {
            referenceDataCacheInvalidator.Invalidated += HandleReferenceDataInvalidated;
        }
    }

    public bool HasPreparedEntries
    {
        get
        {
            lock (gate)
            {
                PruneExpiredCore(timeProvider.GetUtcNow());
                return preparedEntries.Count > 0;
            }
        }
    }

    public void Configure(FloatingAgentChatSettings settings)
    {
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        lock (gate)
        {
            this.settings = settings;
            if (settings.MaximumPreparedAgents == 0)
            {
                preparedEntries.Clear();
            }
            else
            {
                TrimToCapacityCore();
            }
        }
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        FloatingAgentChatSettings currentSettings;
        lock (gate)
        {
            currentSettings = settings;
        }

        if (currentSettings.MaximumPreparedAgents == 0)
        {
            return;
        }

        await RefreshAsync(requestedAgentId: null, cancellationToken);
    }

    public async Task<AgentDefinition?> AcquireAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            PruneExpiredCore(now);
            if (preparedEntries.TryGetValue(agentId, out var entry) &&
                now - entry.ValidatedAtUtc <= MaximumCatalogValidationAge)
            {
                cacheHits++;
                usageCounts[agentId] = ResolveUsageCount(agentId) + 1;
                preparedEntries[agentId] = entry with { LastAcquiredAtUtc = now };
                return entry.Agent;
            }

            cacheMisses++;
        }

        return await RefreshAsync(agentId, cancellationToken);
    }

    public int PruneExpired()
    {
        lock (gate)
        {
            return PruneExpiredCore(timeProvider.GetUtcNow());
        }
    }

    public AgentChatPreparationPoolSnapshot Snapshot()
    {
        lock (gate)
        {
            PruneExpiredCore(timeProvider.GetUtcNow());
            return new AgentChatPreparationPoolSnapshot(
                settings.MaximumPreparedAgents,
                preparedEntries.Count,
                cacheHits,
                cacheMisses,
                preparedEntries.Keys.Order().ToArray());
        }
    }

    private async Task<AgentDefinition?> RefreshAsync(
        Guid? requestedAgentId,
        CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var now = timeProvider.GetUtcNow();
                long refreshVersion;
                lock (gate)
                {
                    refreshVersion = invalidationVersion;
                    if (requestedAgentId.HasValue &&
                        preparedEntries.TryGetValue(requestedAgentId.Value, out var refreshedEntry) &&
                        now - refreshedEntry.ValidatedAtUtc <= MaximumCatalogValidationAge)
                    {
                        cacheHits++;
                        usageCounts[requestedAgentId.Value] = ResolveUsageCount(requestedAgentId.Value) + 1;
                        preparedEntries[requestedAgentId.Value] = refreshedEntry with { LastAcquiredAtUtc = now };
                        return refreshedEntry.Agent;
                    }
                }

                var referenceData = await referenceDataProvider.GetAsync(
                    new AgentReferenceDataRequest(
                        AgentReferenceDataSections.Agents,
                        IncludeAgentTemplates: false,
                        ActiveAgentsOnly: true),
                    cancellationToken);
                var activeAgents = referenceData.Agents
                    .Where(agent => agent.Status == AgentLifecycleStatus.Active && !agent.IsTemplate)
                    .ToArray();
                var requestedAgent = requestedAgentId.HasValue
                    ? activeAgents.FirstOrDefault(agent => agent.Id == requestedAgentId.Value)
                    : null;

                lock (gate)
                {
                    if (refreshVersion != invalidationVersion)
                    {
                        continue;
                    }

                    if (requestedAgentId.HasValue)
                    {
                        usageCounts[requestedAgentId.Value] = ResolveUsageCount(requestedAgentId.Value) + 1;
                    }

                    RebuildPreparedEntriesCore(
                        activeAgents,
                        referenceData.LoadedAtUtc,
                        now,
                        requestedAgentId);
                }

                return requestedAgent;
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private void RebuildPreparedEntriesCore(
        IReadOnlyList<AgentDefinition> activeAgents,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset acquiredAtUtc,
        Guid? acquiredAgentId)
    {
        var activeAgentIds = activeAgents.Select(agent => agent.Id).ToHashSet();
        foreach (var removedAgentId in usageCounts.Keys
                     .Where(agentId => !activeAgentIds.Contains(agentId))
                     .ToArray())
        {
            usageCounts.Remove(removedAgentId);
        }

        Dictionary<Guid, PreparedEntry> previousEntries = new(preparedEntries);
        preparedEntries.Clear();
        if (settings.MaximumPreparedAgents == 0)
        {
            return;
        }

        var candidates = settings.AdaptivePreparationEnabled
            ? activeAgents
                .OrderByDescending(agent => ResolveUsageCount(agent.Id))
                .ThenByDescending(agent => agent.UpdatedAtUtc)
                .ThenBy(agent => agent.Id)
            : activeAgents
                .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(agent => agent.Id);
        foreach (var agent in candidates.Take(settings.MaximumPreparedAgents))
        {
            var lastAcquiredAtUtc = acquiredAgentId == agent.Id
                ? acquiredAtUtc
                : previousEntries.TryGetValue(agent.Id, out var previousEntry)
                    ? previousEntry.LastAcquiredAtUtc
                    : acquiredAtUtc;
            preparedEntries[agent.Id] = new PreparedEntry(
                agent,
                validatedAtUtc,
                lastAcquiredAtUtc);
        }
    }

    private void HandleReferenceDataInvalidated(object? sender, EventArgs eventArgs)
    {
        lock (gate)
        {
            invalidationVersion++;
            preparedEntries.Clear();
        }
    }

    private int PruneExpiredCore(DateTimeOffset now)
    {
        var expiredAgentIds = preparedEntries
            .Where(item => now - item.Value.LastAcquiredAtUtc >= settings.PreparedResourceIdleRetention)
            .Select(item => item.Key)
            .ToArray();
        foreach (var agentId in expiredAgentIds)
        {
            preparedEntries.Remove(agentId);
        }

        return expiredAgentIds.Length;
    }

    private void TrimToCapacityCore()
    {
        foreach (var agentId in preparedEntries
                     .OrderByDescending(item => ResolveUsageCount(item.Key))
                     .ThenByDescending(item => item.Value.LastAcquiredAtUtc)
                     .Skip(settings.MaximumPreparedAgents)
                     .Select(item => item.Key)
                     .ToArray())
        {
            preparedEntries.Remove(agentId);
        }
    }

    private long ResolveUsageCount(Guid agentId)
        => usageCounts.GetValueOrDefault(agentId);

    public void Dispose()
    {
        if (referenceDataCacheInvalidator is not null)
        {
            referenceDataCacheInvalidator.Invalidated -= HandleReferenceDataInvalidated;
        }

        refreshGate.Dispose();
    }

    private sealed record PreparedEntry(
        AgentDefinition Agent,
        DateTimeOffset ValidatedAtUtc,
        DateTimeOffset LastAcquiredAtUtc);
}

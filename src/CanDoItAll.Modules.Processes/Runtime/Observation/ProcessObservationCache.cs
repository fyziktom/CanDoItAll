using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessObservationCache :
    IProcessObservationInvalidator,
    IDisposable
{
    private readonly MemoryCache cache;
    private readonly ILogger<ProcessObservationCache> logger;
    private readonly ConcurrentDictionary<ProcessObservationCacheKey, SemaphoreSlim> keyLocks = new();
    private readonly object indexGate = new();
    private readonly Dictionary<Guid, HashSet<ProcessObservationCacheKey>> keysByDefinitionId = [];
    private readonly Dictionary<Guid, HashSet<ProcessObservationCacheKey>> keysByRunId = [];
    private readonly Dictionary<Guid, HashSet<ProcessObservationCacheKey>> keysByProjectId = [];
    private readonly HashSet<ProcessObservationCacheKey> globalKeys = [];

    public ProcessObservationCache(
        IOptions<ProcessObservationCacheOptions> options,
        ILogger<ProcessObservationCache> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        cache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = Math.Max(64, options.Value.SizeLimit)
            });
    }

    public async Task<ProcessObservationCacheResult<T>> GetOrCreateAsync<T>(
        ProcessObservationCacheKey key,
        ProcessObservationCachePolicy policy,
        Func<CancellationToken, Task<T>> factory,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!bypassCache && TryGetEntry<T>(key, out var cachedEntry))
        {
            logger.LogDebug(
                "Process observation cache hit for {CacheKind}. ProjectId={ProjectId} DefinitionId={DefinitionId} RunId={RunId}.",
                key.Kind,
                key.ProjectId,
                key.DefinitionId,
                key.RunId);
            return new ProcessObservationCacheResult<T>(
                cachedEntry.Value,
                ProcessObservationCacheStatus.Hit,
                cachedEntry.StoredAtUtc,
                cachedEntry.ExpiresAtUtc);
        }

        var keyLock = keyLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            if (!bypassCache && TryGetEntry<T>(key, out cachedEntry))
            {
                return new ProcessObservationCacheResult<T>(
                    cachedEntry.Value,
                    ProcessObservationCacheStatus.Hit,
                    cachedEntry.StoredAtUtc,
                    cachedEntry.ExpiresAtUtc);
            }

            var value = await factory(cancellationToken);
            var storedAtUtc = DateTimeOffset.UtcNow;
            var expiresAtUtc = storedAtUtc.Add(policy.AbsoluteExpiration);
            var entry = new ProcessObservationCacheEntry<T>(value, storedAtUtc, expiresAtUtc);
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSize(Math.Max(1, policy.Size))
                .SetAbsoluteExpiration(policy.AbsoluteExpiration)
                .SetSlidingExpiration(policy.SlidingExpiration)
                .RegisterPostEvictionCallback((evictedKey, _, _, _) =>
                {
                    if (evictedKey is ProcessObservationCacheKey observationKey)
                    {
                        RemoveTrackedKey(observationKey);
                    }
                });

            cache.Set(key, entry, cacheOptions);
            TrackKey(key);
            logger.LogDebug(
                "Process observation cache miss for {CacheKind}. Stored projection until {ExpiresAtUtc}. ProjectId={ProjectId} DefinitionId={DefinitionId} RunId={RunId}.",
                key.Kind,
                expiresAtUtc,
                key.ProjectId,
                key.DefinitionId,
                key.RunId);

            return new ProcessObservationCacheResult<T>(
                value,
                ProcessObservationCacheStatus.Miss,
                storedAtUtc,
                expiresAtUtc);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Process observation cache factory failed for {CacheKind}. ProjectId={ProjectId} DefinitionId={DefinitionId} RunId={RunId}.",
                key.Kind,
                key.ProjectId,
                key.DefinitionId,
                key.RunId);
            throw;
        }
        finally
        {
            keyLock.Release();
        }
    }

    public void NotifyDefinitionChanged(ProcessDefinitionObservationKey key)
    {
        if (key.DefinitionId == Guid.Empty)
        {
            NotifyProjectChanged(key.ProjectId);
            return;
        }

        Invalidate(CollectKeys(key.ProjectId, key.DefinitionId, runId: null));
    }

    public void NotifyRunChanged(ProcessRunObservationKey key)
    {
        if (key.RunId == Guid.Empty)
        {
            NotifyDefinitionChanged(new ProcessDefinitionObservationKey(key.ProjectId, key.DefinitionId));
            return;
        }

        Invalidate(CollectKeys(key.ProjectId, key.DefinitionId, key.RunId));
    }

    public void NotifyProjectChanged(Guid? projectId)
    {
        Invalidate(CollectKeys(projectId, definitionId: null, runId: null));
    }

    public void NotifyAgentExecutionChanged(Guid? processRunId, Guid? processStepRunId)
    {
        if (processRunId.HasValue)
        {
            Invalidate(CollectKeys(projectId: null, definitionId: null, processRunId.Value));
            return;
        }

        Clear();
    }

    public void Clear()
    {
        List<ProcessObservationCacheKey> keys;
        lock (indexGate)
        {
            keys = globalKeys.ToList();
        }

        Invalidate(keys);
    }

    public void Dispose()
    {
        cache.Dispose();
        foreach (var keyLock in keyLocks.Values)
        {
            keyLock.Dispose();
        }
    }

    private bool TryGetEntry<T>(
        ProcessObservationCacheKey key,
        out ProcessObservationCacheEntry<T> entry)
    {
        if (cache.TryGetValue(key, out ProcessObservationCacheEntry<T>? cachedEntry) && cachedEntry is not null)
        {
            entry = cachedEntry;
            return true;
        }

        entry = default!;
        return false;
    }

    private List<ProcessObservationCacheKey> CollectKeys(
        Guid? projectId,
        Guid? definitionId,
        Guid? runId)
    {
        lock (indexGate)
        {
            var keys = new HashSet<ProcessObservationCacheKey>();
            if (projectId.HasValue && keysByProjectId.TryGetValue(projectId.Value, out var projectKeys))
            {
                keys.UnionWith(projectKeys);
            }

            if (definitionId.HasValue && keysByDefinitionId.TryGetValue(definitionId.Value, out var definitionKeys))
            {
                keys.UnionWith(definitionKeys);
            }

            if (runId.HasValue && keysByRunId.TryGetValue(runId.Value, out var runKeys))
            {
                keys.UnionWith(runKeys);
            }

            if (!projectId.HasValue && !definitionId.HasValue && !runId.HasValue)
            {
                keys.UnionWith(globalKeys);
            }

            return keys.ToList();
        }
    }

    private void Invalidate(IReadOnlyCollection<ProcessObservationCacheKey> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            cache.Remove(key);
            RemoveTrackedKey(key);
        }

        logger.LogDebug("Invalidated {CacheEntryCount} process observation cache entries.", keys.Count);
    }

    private void TrackKey(ProcessObservationCacheKey key)
    {
        lock (indexGate)
        {
            globalKeys.Add(key);
            if (key.ProjectId.HasValue)
            {
                Track(keysByProjectId, key.ProjectId.Value, key);
            }

            if (key.DefinitionId.HasValue)
            {
                Track(keysByDefinitionId, key.DefinitionId.Value, key);
            }

            if (key.RunId.HasValue)
            {
                Track(keysByRunId, key.RunId.Value, key);
            }
        }
    }

    private void RemoveTrackedKey(ProcessObservationCacheKey key)
    {
        keyLocks.TryRemove(key, out _);

        lock (indexGate)
        {
            globalKeys.Remove(key);
            if (key.ProjectId.HasValue)
            {
                RemoveTrackedKey(keysByProjectId, key.ProjectId.Value, key);
            }

            if (key.DefinitionId.HasValue)
            {
                RemoveTrackedKey(keysByDefinitionId, key.DefinitionId.Value, key);
            }

            if (key.RunId.HasValue)
            {
                RemoveTrackedKey(keysByRunId, key.RunId.Value, key);
            }
        }
    }

    private static void Track(
        Dictionary<Guid, HashSet<ProcessObservationCacheKey>> index,
        Guid id,
        ProcessObservationCacheKey key)
    {
        if (!index.TryGetValue(id, out var keys))
        {
            keys = [];
            index[id] = keys;
        }

        keys.Add(key);
    }

    private static void RemoveTrackedKey(
        Dictionary<Guid, HashSet<ProcessObservationCacheKey>> index,
        Guid id,
        ProcessObservationCacheKey key)
    {
        if (!index.TryGetValue(id, out var keys))
        {
            return;
        }

        keys.Remove(key);
        if (keys.Count == 0)
        {
            index.Remove(id);
        }
    }
}

internal sealed record ProcessObservationCacheKey(
    ProcessObservationCacheKind Kind,
    Guid? ProjectId,
    Guid? DefinitionId,
    Guid? RunId,
    Guid? StepRunId,
    ProcessObservationDefinitionSetKey DefinitionSetKey,
    string QueryFingerprint);

internal enum ProcessObservationCacheKind
{
    Dashboard,
    LiveSnapshot,
    RunSnapshot,
    StageSnapshot,
    TimelinePage,
    DialogPayload
}

internal sealed record ProcessObservationCachePolicy(
    TimeSpan AbsoluteExpiration,
    TimeSpan SlidingExpiration,
    long Size);

internal sealed record ProcessObservationCacheEntry<T>(
    T Value,
    DateTimeOffset StoredAtUtc,
    DateTimeOffset ExpiresAtUtc);

internal sealed record ProcessObservationCacheResult<T>(
    T Value,
    ProcessObservationCacheStatus Status,
    DateTimeOffset StoredAtUtc,
    DateTimeOffset ExpiresAtUtc);

internal enum ProcessObservationCacheStatus
{
    Hit,
    Miss
}

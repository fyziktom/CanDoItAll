using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessObservationCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_reuses_cached_value_until_run_is_invalidated()
    {
        using var cache = CreateCache();
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var key = CreateRunKey(projectId, definitionId, runId, "run-details");
        var calls = 0;

        var first = await cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            bypassCache: false,
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            bypassCache: false,
            CancellationToken.None);

        cache.NotifyRunChanged(new ProcessRunObservationKey(projectId, definitionId, runId));

        var third = await cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            bypassCache: false,
            CancellationToken.None);

        Assert.Equal(ProcessObservationCacheStatus.Miss, first.Status);
        Assert.Equal(ProcessObservationCacheStatus.Hit, second.Status);
        Assert.Equal(ProcessObservationCacheStatus.Miss, third.Status);
        Assert.Equal(1, first.Value);
        Assert.Equal(1, second.Value);
        Assert.Equal(2, third.Value);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_does_not_cache_factory_failures()
    {
        using var cache = CreateCache();
        var key = CreateRunKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "failure");
        var shouldFail = true;
        var calls = 0;

        Task<int> Factory(CancellationToken _)
        {
            calls++;
            if (shouldFail)
            {
                throw new InvalidOperationException("Observation projection failed.");
            }

            return Task.FromResult(42);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            Factory,
            bypassCache: false,
            CancellationToken.None));

        shouldFail = false;
        var firstSuccess = await cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            Factory,
            bypassCache: false,
            CancellationToken.None);
        var cachedSuccess = await cache.GetOrCreateAsync(
            key,
            CreatePolicy(),
            Factory,
            bypassCache: false,
            CancellationToken.None);

        Assert.Equal(ProcessObservationCacheStatus.Miss, firstSuccess.Status);
        Assert.Equal(ProcessObservationCacheStatus.Hit, cachedSuccess.Status);
        Assert.Equal(42, firstSuccess.Value);
        Assert.Equal(42, cachedSuccess.Value);
        Assert.Equal(2, calls);
    }

    private static ProcessObservationCache CreateCache()
    {
        return new ProcessObservationCache(
            Options.Create(new ProcessObservationCacheOptions
            {
                SizeLimit = 128
            }),
            NullLogger<ProcessObservationCache>.Instance);
    }

    private static ProcessObservationCachePolicy CreatePolicy()
    {
        return new ProcessObservationCachePolicy(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            1);
    }

    private static ProcessObservationCacheKey CreateRunKey(
        Guid projectId,
        Guid definitionId,
        Guid runId,
        string fingerprint)
    {
        return new ProcessObservationCacheKey(
            ProcessObservationCacheKind.RunSnapshot,
            projectId,
            definitionId,
            runId,
            null,
            ProcessObservationDefinitionSetKey.From([definitionId]),
            fingerprint);
    }
}

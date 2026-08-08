using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Bounded, request-scoped lease registry for the exact <see cref="AgentRuntimeTransientContext"/>
/// object captured for one execution run (SB15 rename of the former
/// <c>AgentRunTransientContextRegistry</c>). Digest validation, capacity, and fail-closed
/// <see cref="Resolve"/> semantics are unchanged from that type. New in SB15: a per-entry lease
/// timestamp and a TTL-based eviction backstop that never removes a lease for a run whose most
/// recently observed state is <see cref="ExecutionState.WaitingOnTool"/> — terminal cleanup
/// (<see cref="Remove"/>) remains the primary, immediate cleanup path; TTL eviction only catches
/// leases that primary cleanup somehow missed.
/// </summary>
internal sealed class AgentTurnContextLeaseRegistry
{
    private const int MaximumEntries = 64;

    /// <summary>Generous default: long enough that no realistic human approval delay trips it while an entry's last-observed state still allows eviction.</summary>
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(24);

    /// <summary>
    /// Abandoned-waiting-run reconciliation cutoff: a lease whose run was last
    /// observed as <see cref="ExecutionState.WaitingOnTool"/> is protected from
    /// ordinary TTL eviction, but once nothing has observed the run for this
    /// long the waiting run is treated as abandoned and its lease is
    /// reconciled away so it cannot exhaust the bounded registry. Continuation
    /// after reconciliation stays fail-closed: the pending approvals remain
    /// durable, and resolving the evicted lease raises
    /// <see cref="AgentRunTransientContextUnavailableException"/> instead of
    /// recapturing or guessing context.
    /// </summary>
    public static readonly TimeSpan DefaultAbandonedWaitingRunCutoff = TimeSpan.FromDays(7);

    private readonly object gate = new();
    private readonly Dictionary<Guid, LeaseEntry> leases = [];
    private readonly TimeSpan timeToLive;
    private readonly TimeSpan abandonedWaitingRunCutoff;
    private readonly Action<AgentTurnContextLeaseEvictionDiagnostic>? onEvicted;

    public AgentTurnContextLeaseRegistry(
        TimeSpan? timeToLive = null,
        Action<AgentTurnContextLeaseEvictionDiagnostic>? onEvicted = null,
        TimeSpan? abandonedWaitingRunCutoff = null)
    {
        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeToLive),
                timeToLive,
                "A lease time-to-live must be positive.");
        }

        this.timeToLive = timeToLive ?? DefaultTimeToLive;
        // The default cutoff always covers a custom longer TTL; an explicit
        // cutoff must not undercut the ordinary lease lifetime.
        var resolvedCutoff = abandonedWaitingRunCutoff
            ?? (DefaultAbandonedWaitingRunCutoff > this.timeToLive
                ? DefaultAbandonedWaitingRunCutoff
                : this.timeToLive);
        if (resolvedCutoff < this.timeToLive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(abandonedWaitingRunCutoff),
                resolvedCutoff,
                "The abandoned-waiting-run cutoff cannot be shorter than the ordinary lease time-to-live.");
        }

        this.abandonedWaitingRunCutoff = resolvedCutoff;
        this.onEvicted = onEvicted;
    }

    private sealed record LeaseEntry(
        AgentRuntimeTransientContext Context,
        DateTimeOffset RegisteredAtUtc,
        ExecutionState LastObservedState,
        DateTimeOffset LastObservedAtUtc);

    public void Register(
        ExecutionRunRecord run,
        AgentRuntimeTransientContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);

        var expectedDigest = ExecutionInvocationMetadata.ResolveTransientContextDigest(run);
        if (string.IsNullOrWhiteSpace(expectedDigest))
        {
            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' does not declare a transient context digest.");
        }

        var actualDigest = AgentChatContextDigest.Compute(context);
        if (!string.Equals(expectedDigest, actualDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' transient context does not match its captured context digest.");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        lock (gate)
        {
            EvictExpiredLocked(nowUtc);

            if (leases.TryGetValue(run.Id, out var existing))
            {
                if (!ReferenceEquals(existing.Context, context))
                {
                    throw new InvalidOperationException(
                        $"Execution run '{run.Id:N}' already has a different transient context lease.");
                }

                leases[run.Id] = existing with { LastObservedState = run.State, LastObservedAtUtc = nowUtc };
                return;
            }

            if (leases.Count >= MaximumEntries)
            {
                throw new InvalidOperationException(
                    $"No more than {MaximumEntries} execution runs can retain approval context in one workspace service.");
            }

            leases.Add(run.Id, new LeaseEntry(context, nowUtc, run.State, nowUtc));
        }
    }

    public AgentRuntimeTransientContext? Resolve(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!ExecutionInvocationMetadata.RequiresTransientContext(run))
        {
            return null;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        lock (gate)
        {
            EvictExpiredLocked(nowUtc);

            if (leases.TryGetValue(run.Id, out var entry))
            {
                leases[run.Id] = entry with { LastObservedState = run.State, LastObservedAtUtc = nowUtc };
                return entry.Context;
            }
        }

        throw new AgentRunTransientContextUnavailableException(run.Id);
    }

    public void Remove(Guid executionRunId)
    {
        if (executionRunId == Guid.Empty)
        {
            return;
        }

        lock (gate)
        {
            leases.Remove(executionRunId);
        }
    }

    /// <summary>
    /// Removes leases older than the configured TTL. A lease whose
    /// last-observed state is <see cref="ExecutionState.WaitingOnTool"/> is
    /// protected from ordinary TTL eviction, but not indefinitely: once no
    /// observation has touched the waiting run for the abandoned-waiting-run
    /// cutoff, the lease is reconciled away so abandoned continuations cannot
    /// exhaust the bounded registry. Must be called with <see cref="gate"/>
    /// held.
    /// </summary>
    private void EvictExpiredLocked(DateTimeOffset nowUtc)
    {
        List<(Guid RunId, TimeSpan Age)>? expired = null;
        foreach (var (runId, entry) in leases)
        {
            var age = nowUtc - entry.RegisteredAtUtc;
            if (entry.LastObservedState == ExecutionState.WaitingOnTool)
            {
                var idleTime = nowUtc - entry.LastObservedAtUtc;
                if (idleTime < abandonedWaitingRunCutoff)
                {
                    continue;
                }
            }
            else if (age < timeToLive)
            {
                continue;
            }

            expired ??= [];
            expired.Add((runId, age));
        }

        if (expired is null)
        {
            return;
        }

        foreach (var (runId, age) in expired)
        {
            leases.Remove(runId);
            onEvicted?.Invoke(new AgentTurnContextLeaseEvictionDiagnostic(runId, age));
        }
    }
}

/// <summary>Eviction diagnostic: identity and age only, never the leased payload.</summary>
internal readonly record struct AgentTurnContextLeaseEvictionDiagnostic(
    Guid ExecutionRunId,
    TimeSpan Age);

internal sealed class AgentRunTransientContextUnavailableException(Guid executionRunId)
    : InvalidOperationException(
        $"Execution run '{executionRunId:N}' requires its original application context to continue, but that bounded context lease is no longer available. Start a new message from the current application surface instead of continuing with potentially stale context.")
{
}

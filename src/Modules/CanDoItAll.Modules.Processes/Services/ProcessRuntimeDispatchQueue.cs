using System.Collections.Concurrent;
using System.Threading.Channels;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeDispatchQueue : IProcessRuntimeDispatchQueue
{
    private readonly Channel<ProcessRuntimeDispatchQueueRequest> immediateChannel;
    private readonly Channel<ProcessRuntimeDispatchQueueRequest> recoveryChannel;
    private readonly ConcurrentDictionary<ProcessRunId, byte> immediateQueuedRunIds = new();
    private readonly ConcurrentDictionary<ProcessRunId, byte> recoveryQueuedRunIds = new();
    private readonly object activeRunGate = new();
    private readonly HashSet<ProcessRunId> activeRunIds = [];
    private readonly Dictionary<ProcessRunId, DeferredDispatchRequest> deferredRunRequests = [];
    private readonly Dictionary<ProcessRunId, int> dispatchFailureCounts = [];
    private static readonly TimeSpan InitialDispatchFailureRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumDispatchFailureRetryDelay = TimeSpan.FromSeconds(30);
    private const int MaximumDispatchFailureBackoffLevel = 6;

    public ProcessRuntimeDispatchQueue()
        : this(new ProcessRuntimeDispatchQueueOptions())
    {
    }

    public ProcessRuntimeDispatchQueue(IOptions<ProcessRuntimeDispatchQueueOptions> options)
        : this((options ?? throw new ArgumentNullException(nameof(options))).Value)
    {
    }

    internal ProcessRuntimeDispatchQueue(ProcessRuntimeDispatchQueueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        immediateChannel = CreateChannel(NormalizeCapacity(options.ImmediateQueueCapacity, nameof(options.ImmediateQueueCapacity)));
        recoveryChannel = CreateChannel(NormalizeCapacity(options.RecoveryQueueCapacity, nameof(options.RecoveryQueueCapacity)));
    }

    public async ValueTask EnqueueAsync(
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queuedRunIds = request.IsRecovery
            ? recoveryQueuedRunIds
            : immediateQueuedRunIds;
        if (!queuedRunIds.TryAdd(request.RunId, 0))
        {
            return;
        }

        var channel = request.IsRecovery
            ? recoveryChannel
            : immediateChannel;

        try
        {
            await channel.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            queuedRunIds.TryRemove(request.RunId, out _);
            throw;
        }
    }

    public void EnqueueOrDefer(ProcessRuntimeDispatchQueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TryEnqueue(request))
        {
            lock (activeRunGate)
            {
                deferredRunRequests.Remove(request.RunId);
            }

            return;
        }

        lock (activeRunGate)
        {
            deferredRunRequests[request.RunId] = deferredRunRequests.TryGetValue(
                request.RunId,
                out var existing)
                ? SelectPreferredRequest(existing, new DeferredDispatchRequest(request, DateTimeOffset.MinValue))
                : new DeferredDispatchRequest(request, DateTimeOffset.MinValue);
        }
    }

    public void DeferAfterFailure(
        ProcessRuntimeDispatchQueueRequest request,
        DateTimeOffset failedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (failedAtUtc.Offset != TimeSpan.Zero)
        {
            failedAtUtc = failedAtUtc.ToUniversalTime();
        }

        lock (activeRunGate)
        {
            var failureCount = dispatchFailureCounts.TryGetValue(request.RunId, out var existingFailureCount)
                ? Math.Min(existingFailureCount + 1, MaximumDispatchFailureBackoffLevel)
                : 1;
            dispatchFailureCounts[request.RunId] = failureCount;

            if (immediateQueuedRunIds.ContainsKey(request.RunId) ||
                recoveryQueuedRunIds.ContainsKey(request.RunId) ||
                deferredRunRequests.ContainsKey(request.RunId))
            {
                return;
            }

            deferredRunRequests[request.RunId] = new DeferredDispatchRequest(
                request,
                failedAtUtc.Add(CalculateDispatchFailureRetryDelay(failureCount)));
        }
    }

    public void MarkDispatchSucceeded(ProcessRunId runId)
    {
        lock (activeRunGate)
        {
            dispatchFailureCounts.Remove(runId);
        }
    }

    public bool TryDequeueImmediate(out ProcessRuntimeDispatchQueueRequest request)
    {
        if (immediateChannel.Reader.TryRead(out request!))
        {
            immediateQueuedRunIds.TryRemove(request.RunId, out _);
            return true;
        }

        request = default!;
        return false;
    }

    public bool TryDequeueRecovery(out ProcessRuntimeDispatchQueueRequest request)
    {
        if (recoveryChannel.Reader.TryRead(out request!))
        {
            recoveryQueuedRunIds.TryRemove(request.RunId, out _);
            return true;
        }

        request = default!;
        return false;
    }

    public bool TryMarkActive(ProcessRunId runId)
    {
        lock (activeRunGate)
        {
            return activeRunIds.Add(runId);
        }
    }

    public bool TryMarkActiveOrDefer(ProcessRuntimeDispatchQueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (activeRunGate)
        {
            if (activeRunIds.Add(request.RunId))
            {
                return true;
            }

            deferredRunRequests[request.RunId] = deferredRunRequests.TryGetValue(
                request.RunId,
                out var existing)
                ? SelectPreferredRequest(existing, new DeferredDispatchRequest(request, DateTimeOffset.MinValue))
                : new DeferredDispatchRequest(request, DateTimeOffset.MinValue);
            return false;
        }
    }

    public void MarkInactive(ProcessRunId runId)
    {
        lock (activeRunGate)
        {
            activeRunIds.Remove(runId);
        }
    }

    public int FlushDeferredRequests()
        => FlushDeferredRequests(DateTimeOffset.UtcNow);

    internal int FlushDeferredRequests(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            nowUtc = nowUtc.ToUniversalTime();
        }

        KeyValuePair<ProcessRunId, DeferredDispatchRequest>[] readyRequests;
        lock (activeRunGate)
        {
            readyRequests = deferredRunRequests
                .Where(item =>
                    !activeRunIds.Contains(item.Key) &&
                    item.Value.NotBeforeUtc <= nowUtc)
                .ToArray();
            foreach (var readyRequest in readyRequests)
            {
                deferredRunRequests.Remove(readyRequest.Key);
            }
        }

        var flushedCount = 0;
        foreach (var readyRequest in readyRequests)
        {
            if (TryEnqueue(readyRequest.Value.Request))
            {
                flushedCount++;
                continue;
            }

            lock (activeRunGate)
            {
                deferredRunRequests[readyRequest.Key] = deferredRunRequests.TryGetValue(
                    readyRequest.Key,
                    out var existing)
                    ? SelectPreferredRequest(existing, readyRequest.Value)
                    : readyRequest.Value;
            }
        }

        return flushedCount;
    }

    private bool TryEnqueue(ProcessRuntimeDispatchQueueRequest request)
    {
        var queuedRunIds = request.IsRecovery
            ? recoveryQueuedRunIds
            : immediateQueuedRunIds;
        if (!queuedRunIds.TryAdd(request.RunId, 0))
        {
            return true;
        }

        var channel = request.IsRecovery
            ? recoveryChannel
            : immediateChannel;
        if (channel.Writer.TryWrite(request))
        {
            return true;
        }

        queuedRunIds.TryRemove(request.RunId, out _);
        return false;
    }

    private static ProcessRuntimeDispatchQueueRequest SelectPreferredRequest(
        ProcessRuntimeDispatchQueueRequest existing,
        ProcessRuntimeDispatchQueueRequest candidate)
    {
        return existing.IsRecovery && !candidate.IsRecovery
            ? candidate
            : existing;
    }

    private static DeferredDispatchRequest SelectPreferredRequest(
        DeferredDispatchRequest existing,
        DeferredDispatchRequest candidate)
    {
        return new DeferredDispatchRequest(
            SelectPreferredRequest(existing.Request, candidate.Request),
            existing.NotBeforeUtc <= candidate.NotBeforeUtc
                ? existing.NotBeforeUtc
                : candidate.NotBeforeUtc);
    }

    private static TimeSpan CalculateDispatchFailureRetryDelay(int failureCount)
    {
        var exponent = Math.Min(
            Math.Max(failureCount - 1, 0),
            MaximumDispatchFailureBackoffLevel - 1);
        var delaySeconds = InitialDispatchFailureRetryDelay.TotalSeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(delaySeconds, MaximumDispatchFailureRetryDelay.TotalSeconds));
    }

    private static Channel<ProcessRuntimeDispatchQueueRequest> CreateChannel(int capacity)
    {
        return Channel.CreateBounded<ProcessRuntimeDispatchQueueRequest>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    private static int NormalizeCapacity(int capacity, string optionName)
    {
        if (capacity <= 0)
        {
            throw new InvalidOperationException($"Process runtime dispatch queue option '{optionName}' must be greater than zero.");
        }

        return capacity;
    }

    private sealed record DeferredDispatchRequest(
        ProcessRuntimeDispatchQueueRequest Request,
        DateTimeOffset NotBeforeUtc);
}

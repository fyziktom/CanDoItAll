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
    private readonly ConcurrentDictionary<ProcessRunId, byte> activeRunIds = new();

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
        => activeRunIds.TryAdd(runId, 0);

    public void MarkInactive(ProcessRunId runId)
        => activeRunIds.TryRemove(runId, out _);

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
}

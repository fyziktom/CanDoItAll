using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEventStreamSessionFactory(
    ILlmChatRuntimeLeaseFactory runtimeLeaseFactory,
    ILlmChatOperationScopeAccessor operationScope,
    LlmChatOperationEventJournal eventJournal,
    LlmChatStreamingOptions options)
{
    public async ValueTask<Result<LlmChatOperationEventStreamSession>> OpenAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        ILlmChatRuntimeLease? lease = null;
        try
        {
            lease = await runtimeLeaseFactory.AcquireAsync(cancellationToken).ConfigureAwait(false);
            EnsureCurrent(lease);
            using var scope = operationScope.Push(new LlmChatOperationExecutionContext(operationId, lease.Identity));
            var page = await eventJournal.ListAfterAsync(operationId, 0, 1, lease.CancellationToken)
                .ConfigureAwait(false);
            EnsureCurrent(lease);
            if (page is null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
                return Result<LlmChatOperationEventStreamSession>.Failure(LlmChatErrors.OperationNotFound());
            }

            return Result<LlmChatOperationEventStreamSession>.Success(new(
                operationId,
                lease,
                operationScope,
                eventJournal,
                options.MaximumReplayPageSize));
        }
        catch (LlmChatRuntimeProfileChangedException)
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }

            return Result<LlmChatOperationEventStreamSession>.Failure(LlmChatErrors.RuntimeProfileChanged());
        }
        catch
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    private static void EnsureCurrent(ILlmChatRuntimeLease lease)
    {
        if (lease.EnsureCurrent().IsFailure)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}

public sealed class LlmChatOperationEventStreamSession : IAsyncDisposable
{
    private readonly LlmChatOperationId operationId;
    private readonly ILlmChatRuntimeLease runtimeLease;
    private readonly ILlmChatOperationScopeAccessor operationScope;
    private readonly LlmChatOperationEventJournal eventJournal;
    private int disposed;

    internal LlmChatOperationEventStreamSession(
        LlmChatOperationId operationId,
        ILlmChatRuntimeLease runtimeLease,
        ILlmChatOperationScopeAccessor operationScope,
        LlmChatOperationEventJournal eventJournal,
        int maximumPageSize)
    {
        this.operationId = operationId;
        this.runtimeLease = runtimeLease;
        this.operationScope = operationScope;
        this.eventJournal = eventJournal;
        MaximumPageSize = maximumPageSize;
    }

    public CancellationToken ProfileLifetime => runtimeLease.CancellationToken;

    public int MaximumPageSize { get; }

    public async ValueTask<LlmChatOperationEventPage> ReadAsync(
        long afterSequence,
        int take,
        TimeSpan maximumWait,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaximumPageSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumWait, TimeSpan.Zero);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            runtimeLease.CancellationToken);
        EnsureCurrent();
        using var scope = operationScope.Push(new LlmChatOperationExecutionContext(
            operationId,
            runtimeLease.Identity));
        var page = await RequirePageAsync(afterSequence, take, lifetime.Token).ConfigureAwait(false);
        if (ShouldReturn(page, afterSequence))
        {
            EnsureCurrent();
            return page;
        }

        await eventJournal.WaitAsync(
            operationId,
            afterSequence,
            maximumWait,
            lifetime.Token).ConfigureAwait(false);
        page = await RequirePageAsync(afterSequence, take, lifetime.Token).ConfigureAwait(false);
        EnsureCurrent();
        return page;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        return runtimeLease.DisposeAsync();
    }

    private Task<LlmChatOperationEventPage> RequirePageAsync(
        long afterSequence,
        int take,
        CancellationToken cancellationToken)
        => RequireAsync(eventJournal.ListAfterAsync(operationId, afterSequence, take, cancellationToken));

    private static async Task<LlmChatOperationEventPage> RequireAsync(
        Task<LlmChatOperationEventPage?> pending)
        => await pending.ConfigureAwait(false)
           ?? throw new InvalidOperationException("The streamed LLM Chat operation no longer exists.");

    private static bool ShouldReturn(LlmChatOperationEventPage page, long afterSequence)
        => page.Events.Count > 0 ||
           page.Operation.IsTerminal ||
           page.Operation.Status == LlmChatOperationStatus.RecoveryRequired ||
           afterSequence > page.LatestSequence ||
           page.EarliestRetainedSequence is { } earliest && afterSequence < earliest - 1;

    private void EnsureCurrent()
    {
        if (runtimeLease.EnsureCurrent().IsFailure)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}

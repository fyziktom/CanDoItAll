using System.Runtime.CompilerServices;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class SharedProviderInvocationAuditFinalizer(
    string requestId,
    SharedProviderRelayOperation operation,
    SharedProviderInvocationAuditService invocationAuditService,
    IClock clock,
    ILogger logger)
{
    private static readonly TimeSpan FinalizationTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(200)
    ];
    private const int MaximumAttempts = 3;
    private readonly object gate = new();
    private Task? finalization;

    public Task SucceededAsync(SharedProviderRelayUsage usage)
        => FinalizeOnceAsync(SharedProviderInvocationOutcome.Succeeded, null, usage);

    public Task FailedAsync(
        SharedProviderFailure failure,
        SharedProviderRelayUsage usage)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Category == SharedProviderFailureCategory.Cancelled
            ? CancelledAsync(usage)
            : FinalizeOnceAsync(
                SharedProviderInvocationOutcome.Failed,
                failure.Category,
                usage);
    }

    public Task CancelledAsync(SharedProviderRelayUsage usage)
        => FinalizeOnceAsync(
            SharedProviderInvocationOutcome.Cancelled,
            SharedProviderFailureCategory.Cancelled,
            usage);

    private Task FinalizeOnceAsync(
        SharedProviderInvocationOutcome outcome,
        SharedProviderFailureCategory? failureCategory,
        SharedProviderRelayUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        lock (gate)
        {
            return finalization ??= PersistAsync(outcome, failureCategory, usage);
        }
    }

    private async Task PersistAsync(
        SharedProviderInvocationOutcome outcome,
        SharedProviderFailureCategory? failureCategory,
        SharedProviderRelayUsage usage)
    {
        var mappedUsage = MapUsage(operation, usage);
        var completion = new SharedProviderInvocationCompletion(
            outcome,
            clock.GetUtcNow(),
            failureCategory,
            mappedUsage.InputTokens,
            mappedUsage.OutputTokens,
            mappedUsage.Completeness,
            Price: null,
            SharedProviderMetadataCompleteness.Unavailable)
        {
            ImageCount = mappedUsage.ImageCount
        };
        using var finalizationCancellation = new CancellationTokenSource(FinalizationTimeout);
        Exception? terminalFailure = null;
        var attempts = 0;
        while (attempts < MaximumAttempts)
        {
            attempts++;
            try
            {
                await invocationAuditService.FinalizeAsync(
                    requestId,
                    completion,
                    finalizationCancellation.Token);
                return;
            }
            catch (Exception exception)
            {
                terminalFailure = exception;
            }

            if (attempts >= MaximumAttempts || finalizationCancellation.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    RetryDelays[attempts - 1],
                    finalizationCancellation.Token);
            }
            catch (OperationCanceledException exception)
            {
                terminalFailure = exception;
                break;
            }
        }

        logger.LogWarning(
            "Shared-provider invocation audit finalization did not complete for request {RequestId} after {AttemptCount} attempt(s); durable recovery remains scheduled.",
            requestId,
            attempts);
        throw new SharedProviderInvocationTerminalizationException(terminalFailure!);
    }

    private static PersistedUsage MapUsage(
        SharedProviderRelayOperation operation,
        SharedProviderRelayUsage usage)
    {
        if (usage.Completeness == SharedProviderRelayUsageCompleteness.Unavailable)
        {
            return PersistedUsage.Unavailable;
        }

        return operation switch
        {
            SharedProviderRelayOperation.ChatCompletions or SharedProviderRelayOperation.Responses
                when !usage.ImageCount.HasValue => new PersistedUsage(
                    usage.InputTokens,
                    usage.OutputTokens,
                    MapCompleteness(usage.Completeness),
                    ImageCount: null),
            SharedProviderRelayOperation.ImageGenerations
                when usage.Completeness == SharedProviderRelayUsageCompleteness.Complete &&
                    usage.ImageCount.HasValue => new PersistedUsage(
                        InputTokens: null,
                        OutputTokens: null,
                        SharedProviderMetadataCompleteness.Complete,
                        usage.ImageCount),
            _ => throw new InvalidOperationException(
                $"Relay usage is incompatible with operation '{operation}'.")
        };
    }

    private static SharedProviderMetadataCompleteness MapCompleteness(
        SharedProviderRelayUsageCompleteness completeness)
        => completeness switch
        {
            SharedProviderRelayUsageCompleteness.Partial =>
                SharedProviderMetadataCompleteness.Partial,
            SharedProviderRelayUsageCompleteness.Complete =>
                SharedProviderMetadataCompleteness.Complete,
            _ => throw new ArgumentOutOfRangeException(nameof(completeness), completeness, null)
        };

    private sealed record PersistedUsage(
        long? InputTokens,
        long? OutputTokens,
        SharedProviderMetadataCompleteness Completeness,
        int? ImageCount)
    {
        public static PersistedUsage Unavailable { get; } = new(
            null,
            null,
            SharedProviderMetadataCompleteness.Unavailable,
            null);
    }
}

internal sealed class SharedProviderInvocationTerminalizationException(Exception innerException) :
    Exception("Shared-provider invocation audit finalization could not be persisted.", innerException);

internal sealed class SharedProviderAuditedRelayStream : ISharedProviderRelayStream
{
    private readonly ISharedProviderRelayStream inner;
    private readonly SharedProviderInvocationAuditFinalizer finalizer;
    private readonly Task<SharedProviderRelayStreamCompletion> completion;

    public SharedProviderAuditedRelayStream(
        ISharedProviderRelayStream inner,
        SharedProviderInvocationAuditFinalizer finalizer)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(finalizer);
        this.inner = inner;
        this.finalizer = finalizer;
        completion = ObserveCompletionAsync();
    }

    public SharedProviderRelayResponseHeaders Headers => inner.Headers;

    public Task<SharedProviderRelayStreamCompletion> Completion => completion;

    public IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        CancellationToken cancellationToken = default)
        => ReadFramesCoreAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await inner.DisposeAsync();
        }
        finally
        {
            await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
        }
    }

    private async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesCoreAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fullyRead = false;
        var terminalFailureRecorded = false;
        try
        {
            await using var enumerator = inner
                .ReadFramesAsync(cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException)
                {
                    terminalFailureRecorded = true;
                    await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
                    throw;
                }
                catch
                {
                    terminalFailureRecorded = true;
                    await finalizer.FailedAsync(
                        SharedProviderRelayFailures.UpstreamFailure,
                        SharedProviderRelayUsage.Unavailable);
                    throw;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return enumerator.Current;
            }

            fullyRead = true;
        }
        finally
        {
            if (!fullyRead && !terminalFailureRecorded)
            {
                await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
            }
        }

        await completion;
    }

    private async Task<SharedProviderRelayStreamCompletion> ObserveCompletionAsync()
    {
        try
        {
            var result = await inner.Completion;
            if (result.Failure is null)
            {
                await finalizer.SucceededAsync(result.Usage);
            }
            else
            {
                await finalizer.FailedAsync(result.Failure, result.Usage);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
            throw;
        }
        catch
        {
            await finalizer.FailedAsync(
                SharedProviderRelayFailures.UpstreamFailure,
                SharedProviderRelayUsage.Unavailable);
            throw;
        }
    }
}

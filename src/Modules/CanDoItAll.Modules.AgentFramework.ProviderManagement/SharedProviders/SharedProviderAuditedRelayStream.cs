using System.Runtime.CompilerServices;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

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
            if (inner.Completion.IsCompleted) {
                await completion;
            } else {
                await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
            }
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
        SharedProviderRelayStreamCompletion result;
        try
        {
            result = await inner.Completion;
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
        if (result.Failure is null) {
            await finalizer.SucceededAsync(result.Usage);
        } else {
            await finalizer.FailedAsync(result.Failure, result.Usage);
        }
        return result;
    }
}

using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatProfileScopeRunner(
    ILlmChatRuntimeLeaseFactory runtimeLeaseFactory,
    ILlmChatOperationScopeAccessor operationScope,
    IServiceScopeFactory serviceScopeFactory) : IDisposable {
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly CancellationTokenSource retirement = new();
    private readonly object lifetimeLock = new();
    private int participants;
    private bool retired;
    private bool retirementSignaled;

    public Task<Result<T>> ExecuteOwnedAsync<TService, T>(
        LlmChatOperationId operationId,
        Func<TService, CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default) where TService : notnull {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteAsync(operationId, async token => {
            await using var dependencies = serviceScopeFactory.CreateAsyncScope();
            return await operation(dependencies.ServiceProvider.GetRequiredService<TService>(), token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        LlmChatOperationId operationId,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);
        lock (lifetimeLock) {
            ObjectDisposedException.ThrowIf(retired, this);
            participants++;
        }
        var admitted = false;
        try {
            using (var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, retirement.Token)) {
                await operationGate.WaitAsync(admission.Token).ConfigureAwait(false);
            }
            admitted = true;
            lock (lifetimeLock) {
                if (retired) {
                    throw new OperationCanceledException(retirement.Token);
                }
            }
            await using var lease = await runtimeLeaseFactory.AcquireAsync(cancellationToken).ConfigureAwait(false);
            EnsureCurrent(lease);
            using var scope = operationScope.Push(new LlmChatOperationExecutionContext(operationId, lease.Identity));
            try {
                var result = await operation(lease.CancellationToken).ConfigureAwait(false);
                EnsureCurrent(lease);
                return result;
            } catch (OperationCanceledException) when (lease.EnsureCurrent().IsFailure) {
                return Result<T>.Failure(LlmChatErrors.RuntimeProfileChanged());
            }
        } catch (LlmChatRuntimeProfileChangedException) {
            return Result<T>.Failure(LlmChatErrors.RuntimeProfileChanged());
        } finally {
            if (admitted) {
                operationGate.Release();
            }
            lock (lifetimeLock) {
                participants--;
                ReleaseRetiredResources();
            }
        }
    }

    public void Dispose() {
        lock (lifetimeLock) {
            if (retired) {
                return;
            }
            retired = true;
        }
        try {
            retirement.Cancel();
        } finally {
            lock (lifetimeLock) {
                retirementSignaled = true;
                ReleaseRetiredResources();
            }
        }
    }

    private void ReleaseRetiredResources() {
        if (retirementSignaled && participants == 0) {
            operationGate.Dispose();
            retirement.Dispose();
        }
    }

    private static void EnsureCurrent(ILlmChatRuntimeLease lease) {
        if (lease.EnsureCurrent().IsFailure) {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}

using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatProfileScopeRunner(
    ILlmChatRuntimeLeaseFactory runtimeLeaseFactory,
    ILlmChatOperationScopeAccessor operationScope) : IDisposable {
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public async Task<Result<T>> ExecuteAsync<T>(
        LlmChatOperationId operationId,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
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
            operationGate.Release();
        }
    }

    public void Dispose() => operationGate.Dispose();

    private static void EnsureCurrent(ILlmChatRuntimeLease lease) {
        if (lease.EnsureCurrent().IsFailure) {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}

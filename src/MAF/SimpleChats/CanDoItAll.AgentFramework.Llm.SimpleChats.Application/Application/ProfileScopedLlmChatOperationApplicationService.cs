using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal sealed class ProfileScopedLlmChatOperationApplicationService(
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatOperationApplicationService
{
    public Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
        => scopeRunner.ExecuteOwnedAsync<LlmChatOperationApplicationService, LlmChatOperationDetails>(
            command.OperationId,
            (inner, token) => inner.SendAsync(command, token),
            cancellationToken);

    public Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, (inner, token) => inner.GetAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, (inner, token) => inner.CancelAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, (inner, token) => inner.ReconcileAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            command.TurnId,
            (inner, token) => inner.AbandonActiveTurnAsync(command, token),
            cancellationToken);

    private Task<Result<LlmChatOperationDetails>> ExecuteAsync(
        LlmChatOperationId operationId,
        Func<LlmChatOperationApplicationService, CancellationToken, Task<Result<LlmChatOperationDetails>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteOwnedAsync<LlmChatOperationApplicationService, LlmChatOperationDetails>(operationId, operation, cancellationToken);
}

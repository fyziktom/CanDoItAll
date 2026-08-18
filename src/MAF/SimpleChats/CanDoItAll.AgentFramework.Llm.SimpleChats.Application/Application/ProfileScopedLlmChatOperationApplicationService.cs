using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal sealed class ProfileScopedLlmChatOperationApplicationService(
    LlmChatOperationApplicationService inner,
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatOperationApplicationService
{
    public Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
        => scopeRunner.ExecuteAsync(
            command.OperationId,
            token => inner.SendAsync(command, token),
            cancellationToken);

    public Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, token => inner.GetAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, token => inner.CancelAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(operationId, token => inner.ReconcileAsync(operationId, token), cancellationToken);

    public Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            command.TurnId,
            token => inner.AbandonActiveTurnAsync(command, token),
            cancellationToken);

    private Task<Result<LlmChatOperationDetails>> ExecuteAsync(
        LlmChatOperationId operationId,
        Func<CancellationToken, Task<Result<LlmChatOperationDetails>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteAsync(operationId, operation, cancellationToken);
}

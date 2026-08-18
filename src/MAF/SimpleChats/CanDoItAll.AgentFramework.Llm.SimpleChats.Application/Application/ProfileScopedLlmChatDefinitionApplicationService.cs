using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal sealed class ProfileScopedLlmChatDefinitionApplicationService(
    LlmChatDefinitionApplicationService inner,
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatDefinitionApplicationService
{
    public Task<Result<LlmChatDefinitionDetails>> CreateAsync(
        CreateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.CreateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
        UpdateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.UpdateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
        ChangeLlmChatDefinitionStatusCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ChangeStatusAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.GetAsync(definitionId, token), cancellationToken);

    public Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ListAsync(query, token), cancellationToken);

    public Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ListPageAsync(query, token), cancellationToken);

    private Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteAsync(LlmChatOperationId.New(), operation, cancellationToken);
}

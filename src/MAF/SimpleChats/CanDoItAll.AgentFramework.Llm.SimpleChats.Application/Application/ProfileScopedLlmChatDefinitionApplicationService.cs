using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal sealed class ProfileScopedLlmChatDefinitionApplicationService(
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatDefinitionApplicationService, ILlmChatDefinitionCreateReceiptService
{
    public Task<Result<LlmChatDefinitionDetails>> CreateAsync(
        CreateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.CreateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionCreateResponse>> CreateOnceAsync(
        CreateLlmChatDefinitionOnceCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.CreateOnceAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionCreateReceipt?>> FindReceiptAsync(
        LlmChatDefinitionCreateKey key,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.FindReceiptAsync(key, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
        UpdateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.UpdateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
        ChangeLlmChatDefinitionStatusCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ChangeStatusAsync(command, token), cancellationToken);

    public Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.GetAsync(definitionId, token), cancellationToken);

    public Task<Result<LlmChatDefinitionRevision>> GetRevisionAsync(
        LlmChatDefinitionId definitionId,
        LlmChatDefinitionRevisionNumber revision,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.GetRevisionAsync(definitionId, revision, token), cancellationToken);

    public Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ListAsync(query, token), cancellationToken);

    public Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ListPageAsync(query, token), cancellationToken);

    private Task<Result<T>> ExecuteAsync<T>(
        Func<LlmChatDefinitionApplicationService, CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteOwnedAsync<LlmChatDefinitionApplicationService, T>(LlmChatOperationId.New(), operation, cancellationToken);
}

using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationDetailsReader(
    ILlmChatOperationRepository operationRepository,
    ILlmChatOperationReadStore readStore,
    ILlmChatConversationEngine conversationEngine)
{
    internal async Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
    {
        var operation = await readStore.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false);
        return operation is null
            ? Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound())
            : await BuildAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Result<LlmChatOperationDetails>> BuildAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken)
    {
        var model = await readStore.TryGetAsync(operation.Id, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
        }

        return await BuildAsync(model, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LlmChatOperationDetails>> BuildAsync(
        LlmChatOperationReadModel model,
        CancellationToken cancellationToken)
    {
        var operation = model.Operation;
        var evidence = await conversationEngine.InspectTurnAsync(
            operation.ConversationId,
            operation.Id,
            cancellationToken).ConfigureAwait(false);
        var assistant = evidence?.Assistant is { } item
            ? new LlmChatAssistantMessage(
                item.EntryId,
                operation.Id,
                item.Text,
                item.Model,
                item.Usage,
                item.CreatedAtUtc)
            : null;
        return Result<LlmChatOperationDetails>.Success(new(operation, assistant, model.Invocations));
    }

    internal async Task<LlmChatOperation> RequireAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException("The admitted LLM Chat operation no longer exists.");
}

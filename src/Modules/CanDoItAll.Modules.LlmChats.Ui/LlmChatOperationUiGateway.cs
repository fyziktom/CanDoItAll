using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Modules.LlmChats.Ui;

public sealed record LlmChatOperationView(
    Guid OperationId,
    Guid ConversationId,
    LlmChatOperationStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? ResultingTranscriptRevision,
    long LastEventSequence,
    string AssistantText,
    string Model,
    LlmChatUiFailure? Failure)
{
    public bool IsTerminal => Status is
        LlmChatOperationStatus.Succeeded or
        LlmChatOperationStatus.Failed or
        LlmChatOperationStatus.Cancelled;

    public bool CanCancel => Status is
        LlmChatOperationStatus.Pending or
        LlmChatOperationStatus.Running;
}

public interface ILlmChatOperationUiGateway
{
    Task<LlmChatUiResult<LlmChatOperationView>> SendAsync(
        Guid operationId,
        Guid conversationId,
        long expectedTranscriptRevision,
        string message,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatOperationView>> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatOperationView>> CancelAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatOperationView>> ReconcileAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatOperationView>> AbandonAsync(
        Guid conversationId,
        Guid operationId,
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatOperationUiGateway(
    ILlmChatOperationApplicationService operations,
    ILlmChatUiAuthorizationFacade authorization) : ILlmChatOperationUiGateway
{
    public async Task<LlmChatUiResult<LlmChatOperationView>> SendAsync(
        Guid operationId,
        Guid conversationId,
        long expectedTranscriptRevision,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Execute, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatOperationView>(LlmChatUiPermission.Execute);
        }

        if (!TryCreateOperationId(operationId, out var typedOperationId) ||
            !TryCreateConversationId(conversationId, out var typedConversationId))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatOperationView>("Select a valid Simple Chat operation and conversation.");
        }

        var result = await operations.SendAsync(
            new(typedOperationId, typedConversationId, expectedTranscriptRevision, message),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    public async Task<LlmChatUiResult<LlmChatOperationView>> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatOperationView>(LlmChatUiPermission.Read);
        }

        if (!TryCreateOperationId(operationId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatOperationView>("Select a valid Simple Chat operation.");
        }

        var result = await operations.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    public Task<LlmChatUiResult<LlmChatOperationView>> CancelAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
        => ExecuteByIdAsync(operationId, operations.CancelAsync, LlmChatUiPermission.Execute, cancellationToken);

    public Task<LlmChatUiResult<LlmChatOperationView>> ReconcileAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
        => ExecuteByIdAsync(operationId, operations.ReconcileAsync, LlmChatUiPermission.Manage, cancellationToken);

    public async Task<LlmChatUiResult<LlmChatOperationView>> AbandonAsync(
        Guid conversationId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Execute, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatOperationView>(LlmChatUiPermission.Execute);
        }

        if (!TryCreateOperationId(operationId, out var typedOperationId) ||
            !TryCreateConversationId(conversationId, out var typedConversationId))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatOperationView>("Select a valid Simple Chat operation and conversation.");
        }

        var result = await operations.AbandonActiveTurnAsync(
            new(typedConversationId, typedOperationId),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    private async Task<LlmChatUiResult<LlmChatOperationView>> ExecuteByIdAsync(
        Guid operationId,
        Func<LlmChatOperationId, CancellationToken, Task<CanDoItAll.SharedKernel.Result<LlmChatOperationDetails>>> execute,
        LlmChatUiPermission permission,
        CancellationToken cancellationToken)
    {
        if (!await authorization.IsAllowedAsync(permission, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatOperationView>(permission);
        }

        if (!TryCreateOperationId(operationId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatOperationView>("Select a valid Simple Chat operation.");
        }

        var result = await execute(id, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    private static bool TryCreateOperationId(Guid value, out LlmChatOperationId id)
    {
        if (value == Guid.Empty)
        {
            id = default;
            return false;
        }

        id = new(value);
        return true;
    }

    private static bool TryCreateConversationId(Guid value, out LlmChatConversationId id)
    {
        if (value == Guid.Empty)
        {
            id = default;
            return false;
        }

        id = new(value);
        return true;
    }

    private static LlmChatOperationView ToView(LlmChatOperationDetails details)
    {
        var operation = details.Operation;
        return new(
            operation.Id.Value,
            operation.ConversationId.Value,
            operation.Status,
            operation.StartedAtUtc,
            operation.CompletedAtUtc,
            operation.ResultingTranscriptRevision,
            details.LastEventSequence,
            details.AssistantMessage?.Content ?? string.Empty,
            details.AssistantMessage?.Model ?? string.Empty,
            string.IsNullOrWhiteSpace(operation.FailureCode)
                ? null
                : LlmChatUiResultMapper.FromFailureCode(operation.FailureCode));
    }
}

using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using Microsoft.Extensions.Logging;
using Operations = CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatConversationWorkspaceController(
    ILlmChatDefinitionUiGateway definitions,
    ILlmChatConversationUiGateway conversations,
    ILlmChatOperationUiGateway operations,
    ILlmChatUiAuthorizationFacade authorization,
    ILogger<LlmChatConversationWorkspaceController> logger,
    TimeProvider timeProvider)
{
    public const int ConversationPageSize = 24;
    public const int MaximumConversationCount = 96;
    public const int DefinitionPageSize = 50;
    public const int MaximumDefinitionCount = 100;
    public const int TranscriptPageSize = 50;
    public const int MaximumMessageCount = 200;

    private readonly LlmChatWorkspacePage<LlmChatConversationListItem, Guid, LlmChatConversationCursor> conversationPage =
        new(item => item.ConversationId, MaximumConversationCount);
    private readonly LlmChatWorkspacePage<LlmChatDefinitionListItem, Guid, LlmChatDefinitionCursor> definitionPage =
        new(item => item.DefinitionId, MaximumDefinitionCount);
    private readonly LlmChatWorkspacePage<LlmChatMessageListItem, Guid, LlmChatTranscriptCursor> transcriptPage =
        new(item => item.EntryId, MaximumMessageCount);
    private readonly LlmChatOperationWorkspaceState operationState = new();

    public LlmChatUiAuthorizationSnapshot Authorization { get; private set; } = new(false, false, false);

    public IReadOnlyList<LlmChatConversationListItem> Conversations => conversationPage.Items;

    public IReadOnlyList<LlmChatDefinitionListItem> ActiveDefinitions => definitionPage.Items;

    public IReadOnlyList<LlmChatMessageListItem> Messages => transcriptPage.Items;

    public LlmChatConversationListItem? SelectedConversation { get; private set; }

    public LlmChatPendingTurn? PendingTurn => operationState.PendingTurn;

    public LlmChatOperationView? ActiveOperation => operationState.ActiveOperation;

    public LlmChatOperationProjectionState? OperationProjection => operationState.Projection;

    public bool HasMoreConversations => conversationPage.HasMore;

    public bool HasMoreDefinitions => definitionPage.HasMore;

    public bool HasMoreMessages => transcriptPage.HasMore;

    public bool IsLoading { get; private set; }

    public bool IsMutating { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public bool CanReloadSelected =>
        SelectedConversation is not null &&
        lastFailureCodes.Any(code => code is
            LlmChatErrorCodes.TranscriptRevisionConflict or
            LlmChatErrorCodes.StorageConflict);

    public bool CanCancel =>
        Authorization.CanExecute &&
        ActiveOperation?.CanCancel == true;

    public bool CanReconcile =>
        Authorization.CanManage &&
        ActiveOperation?.Status == Operations.LlmChatOperationStatus.RecoveryRequired;

    public bool CanAbandon =>
        Authorization.CanExecute &&
        operationState.RecoveryEvidenceConfirmed &&
        ActiveOperation is
        {
            Status: Operations.LlmChatOperationStatus.RecoveryRequired
        } operation &&
        SelectedConversation?.ActiveOperationId == operation.OperationId;

    public string OperationStatusText => operationState.StatusText;

    private IReadOnlyList<string> lastFailureCodes = [];

    public async Task InitializeAsync(
        CancellationToken cancellationToken,
        Guid? preferredConversationId = null)
    {
        Authorization = await authorization.GetAsync(cancellationToken);
        if (!Authorization.CanRead)
        {
            return;
        }

        if (Authorization.CanManage)
        {
            await LoadDefinitionsAsync(append: false, cancellationToken);
        }

        if (!await LoadConversationsAsync(append: false, cancellationToken) || Conversations.Count == 0)
        {
            return;
        }

        await SelectConversationAsync(
            preferredConversationId ?? Conversations[0].ConversationId,
            cancellationToken);
    }

    public Task<bool> LoadMoreConversationsAsync(CancellationToken cancellationToken)
        => HasMoreConversations
            ? LoadConversationsAsync(append: true, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> LoadMoreDefinitionsAsync(CancellationToken cancellationToken)
        => HasMoreDefinitions
            ? LoadDefinitionsAsync(append: true, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> LoadMoreMessagesAsync(CancellationToken cancellationToken)
        => SelectedConversation is not null && HasMoreMessages
            ? LoadTranscriptAsync(SelectedConversation.ConversationId, append: true, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> SelectConversationAsync(Guid conversationId, CancellationToken cancellationToken)
        => LoadTranscriptAsync(conversationId, append: false, cancellationToken);

    public Task<bool> ReloadSelectedAsync(CancellationToken cancellationToken)
        => SelectedConversation is null
            ? Task.FromResult(false)
            : LoadTranscriptAsync(SelectedConversation.ConversationId, append: false, cancellationToken);

    public async Task<Guid?> PrepareActiveOperationAsync(CancellationToken cancellationToken)
    {
        operationState.Prepare();
        if (SelectedConversation?.ActiveOperationId is not { } operationId)
        {
            operationState.Restore(null);
            return null;
        }

        try
        {
            var result = await operations.GetAsync(operationId, cancellationToken);
            if (!TryGetValue(result, out var operation) || !HasExpectedIdentity(operation, operationId))
            {
                return null;
            }

            operationState.Restore(operation);
            return operationId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to restore Simple Chat operation. ConversationId={ConversationId} OperationId={OperationId}.",
                SelectedConversation?.ConversationId,
                operationId);
            SetUnexpectedFailure();
            return null;
        }
    }

    public void ApplyOperationProjection(LlmChatOperationProjectionState projection)
    {
        operationState.ApplyProjection(projection);
    }

    public async Task<bool> RefreshActiveOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (SelectedConversation is null)
        {
            return false;
        }

        var operationResult = await operations.GetAsync(operationId, cancellationToken);
        if (!TryGetValue(operationResult, out var operation) || !HasExpectedIdentity(operation, operationId))
        {
            return false;
        }

        var conversationId = SelectedConversation.ConversationId;
        if (!await LoadTranscriptAsync(conversationId, append: false, cancellationToken))
        {
            return false;
        }

        operationState.CompleteRefresh(operation);
        return true;
    }

    public async Task<Guid?> ReloadForProfileChangeAsync(CancellationToken cancellationToken)
    {
        conversationPage.Clear();
        definitionPage.Clear();
        transcriptPage.Clear();
        SelectedConversation = null;
        operationState.Reset();
        await InitializeAsync(cancellationToken);
        return await PrepareActiveOperationAsync(cancellationToken);
    }

    public void SetFollowerFailure(IReadOnlyList<LlmChatUiFailure> failures)
        => SetFailure(failures);

    public Task<bool> CancelActiveOperationAsync(CancellationToken cancellationToken)
        => MutateActiveOperationAsync(
            "cancel operation",
            CanCancel,
            (operation, token) => operations.CancelAsync(operation.OperationId, token),
            confirmRecoveryEvidence: false,
            refreshTranscript: false,
            cancellationToken);

    public Task<bool> ReconcileActiveOperationAsync(CancellationToken cancellationToken)
        => MutateActiveOperationAsync(
            "reconcile operation",
            CanReconcile,
            (operation, token) => operations.ReconcileAsync(operation.OperationId, token),
            confirmRecoveryEvidence: true,
            refreshTranscript: true,
            cancellationToken);

    public Task<bool> AbandonActiveOperationAsync(CancellationToken cancellationToken)
        => MutateActiveOperationAsync(
            "abandon operation",
            CanAbandon,
            (operation, token) => operations.AbandonAsync(
                operation.ConversationId,
                operation.OperationId,
                token),
            confirmRecoveryEvidence: false,
            refreshTranscript: true,
            cancellationToken);

    public async Task<bool> CreateAsync(
        Guid definitionId,
        string title,
        CancellationToken cancellationToken)
    {
        if (!Authorization.CanManage ||
            !ActiveDefinitions.Any(item =>
                item.DefinitionId == definitionId && item.Status == LlmChatDefinitionStatus.Active))
        {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Choose an active Simple Chat definition."));
            return false;
        }

        return await RunMutationAsync(
            "create conversation",
            async () =>
            {
                var result = await conversations.CreateAsync(definitionId, title.Trim(), cancellationToken);
                if (!TryGetValue(result, out var view))
                {
                    return false;
                }

                ReplaceOrInsertConversation(view.Conversation);
                SelectView(view);
                return true;
            });
    }

    public async Task<bool> RenameSelectedAsync(string title, CancellationToken cancellationToken)
    {
        if (!Authorization.CanManage || SelectedConversation is not { } selected)
        {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Select a conversation you can manage."));
            return false;
        }

        return await RunMutationAsync(
            "rename conversation",
            async () =>
            {
                var result = await conversations.RenameAsync(
                    selected.ConversationId,
                    title.Trim(),
                    selected.ConcurrencyToken,
                    selected.TranscriptRevision,
                    cancellationToken);
                if (!TryGetValue(result, out var view))
                {
                    return false;
                }

                SelectedConversation = view.Conversation;
                ReplaceOrInsertConversation(view.Conversation);
                return true;
            });
    }

    public async Task<bool> ArchiveSelectedAsync(CancellationToken cancellationToken)
    {
        if (!Authorization.CanManage || SelectedConversation is not { } selected)
        {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Select a conversation you can manage."));
            return false;
        }

        return await RunMutationAsync(
            "archive conversation",
            async () =>
            {
                var result = await conversations.ArchiveAsync(
                    selected.ConversationId,
                    selected.ConcurrencyToken,
                    cancellationToken);
                if (!TryGetValue(result, out var view))
                {
                    return false;
                }

                SelectedConversation = view.Conversation;
                ReplaceOrInsertConversation(view.Conversation);
                return true;
            });
    }

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken)
    {
        var normalizedMessage = message.Trim();
        if (!Authorization.CanExecute ||
            SelectedConversation is not { Status: LlmChatConversationStatus.Active } selected ||
            selected.ActiveOperationId.HasValue ||
            string.IsNullOrWhiteSpace(normalizedMessage))
        {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.InvalidInput,
                "Select an active conversation and enter a message."));
            return false;
        }

        var operationId = operationState.GetAdmissionOperationId(selected.ConversationId, normalizedMessage);
        return await RunMutationAsync(
            "admit conversation turn",
            async () =>
            {
                var result = await operations.SendAsync(
                    operationId,
                    selected.ConversationId,
                    selected.TranscriptRevision,
                    normalizedMessage,
                    cancellationToken);
                if (!TryGetValue(result, out var operation))
                {
                    return false;
                }

                if (operation.OperationId != operationId ||
                    operation.ConversationId != selected.ConversationId)
                {
                    logger.LogError(
                        "Simple Chat admission returned mismatched identity. ConversationId={ConversationId} OperationId={OperationId}.",
                        selected.ConversationId,
                        operationId);
                    SetUnexpectedFailure();
                    return false;
                }

                operationState.Start(operation, normalizedMessage, timeProvider.GetUtcNow());
                SelectedConversation = selected with { ActiveOperationId = operationId };
                ReplaceOrInsertConversation(SelectedConversation);
                return true;
            });
    }

    private async Task<bool> LoadConversationsAsync(bool append, CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return false;
        }

        IsLoading = true;
        ClearFailure();
        try
        {
            var result = await conversations.ListPageAsync(new(
                take: ConversationPageSize,
                cursor: append ? conversationPage.NextCursor : null), cancellationToken);
            if (!TryGetValue(result, out var page))
            {
                return false;
            }

            if (append)
            {
                conversationPage.Append(page.Items, page.NextCursor);
            }
            else
            {
                conversationPage.Replace(page.Items, page.NextCursor);
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to list Simple Chat conversations.");
            SetUnexpectedFailure();
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> LoadDefinitionsAsync(bool append, CancellationToken cancellationToken)
    {
        ClearFailure();
        try
        {
            var result = await definitions.ListPageAsync(new(
                take: DefinitionPageSize,
                status: LlmChatDefinitionStatus.Active,
                cursor: append ? definitionPage.NextCursor : null), cancellationToken);
            if (!TryGetValue(result, out var page))
            {
                return false;
            }

            var activeItems = page.Items.Where(item => item.Status == LlmChatDefinitionStatus.Active);
            if (append)
            {
                definitionPage.Append(activeItems, page.NextCursor);
            }
            else
            {
                definitionPage.Replace(activeItems, page.NextCursor);
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to list active Simple Chat definitions.");
            SetUnexpectedFailure();
            return false;
        }
    }

    private async Task<bool> LoadTranscriptAsync(
        Guid conversationId,
        bool append,
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return false;
        }

        IsLoading = true;
        ClearFailure();
        try
        {
            var result = await conversations.GetAsync(
                conversationId,
                new(TranscriptPageSize, append ? transcriptPage.NextCursor : null),
                cancellationToken);
            if (!TryGetValue(result, out var view))
            {
                return false;
            }

            if (view.Conversation.ConversationId != conversationId)
            {
                logger.LogError(
                    "Simple Chat transcript returned mismatched conversation identity. RequestedConversationId={ConversationId}.",
                    conversationId);
                SetUnexpectedFailure();
                return false;
            }

            if (!append)
            {
                transcriptPage.Clear();
                operationState.Reset();
            }

            var visibleMessages = view.Messages.Where(message =>
                message.Role is LlmMessageRole.User or LlmMessageRole.Assistant);
            transcriptPage.Append(visibleMessages, view.NextMessageCursor);
            SelectedConversation = view.Conversation;
            ReplaceOrInsertConversation(view.Conversation);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to load Simple Chat transcript. ConversationId={ConversationId}.",
                conversationId);
            SetUnexpectedFailure();
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> RunMutationAsync(string operationName, Func<Task<bool>> action)
    {
        if (IsMutating)
        {
            return false;
        }

        IsMutating = true;
        ClearFailure();
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to {OperationName} in the Simple Chat workspace. ConversationId={ConversationId}.",
                operationName,
                SelectedConversation?.ConversationId);
            SetUnexpectedFailure();
            return false;
        }
        finally
        {
            IsMutating = false;
        }
    }

    private void SelectView(LlmChatConversationView view)
    {
        transcriptPage.Replace(
            view.Messages.Where(message => message.Role is LlmMessageRole.User or LlmMessageRole.Assistant),
            view.NextMessageCursor);
        SelectedConversation = view.Conversation;
        operationState.Reset();
    }

    private async Task<bool> MutateActiveOperationAsync(
        string operationName,
        bool isAllowed,
        Func<LlmChatOperationView, CancellationToken, Task<LlmChatUiResult<LlmChatOperationView>>> mutate,
        bool confirmRecoveryEvidence,
        bool refreshTranscript,
        CancellationToken cancellationToken)
    {
        if (!isAllowed || ActiveOperation is not { } current)
        {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.InvalidInput,
                "The requested Simple Chat recovery action is not available."));
            return false;
        }

        return await RunMutationAsync(
            operationName,
            async () =>
            {
                var result = await mutate(current, cancellationToken);
                if (!TryGetValue(result, out var operation) ||
                    !HasExpectedIdentity(operation, current.OperationId))
                {
                    return false;
                }

                operationState.Restore(operation);
                if (refreshTranscript && SelectedConversation is { } selected)
                {
                    var refreshed = await LoadTranscriptAsync(
                        selected.ConversationId,
                        append: false,
                        cancellationToken);
                    operationState.CompleteRefresh(operation);
                    if (!refreshed)
                    {
                        return false;
                    }
                }

                operationState.CompleteMutation(operation, confirmRecoveryEvidence);

                return true;
            });
    }

    private bool HasExpectedIdentity(LlmChatOperationView operation, Guid operationId)
    {
        if (SelectedConversation is { } selected &&
            operation.OperationId == operationId &&
            operation.ConversationId == selected.ConversationId)
        {
            return true;
        }

        logger.LogError(
            "Simple Chat operation returned mismatched identity. ConversationId={ConversationId} OperationId={OperationId}.",
            SelectedConversation?.ConversationId,
            operationId);
        SetUnexpectedFailure();
        return false;
    }

    private void ReplaceOrInsertConversation(LlmChatConversationListItem conversation)
    {
        conversationPage.UpsertFirst(conversation);
    }

    private bool TryGetValue<T>(LlmChatUiResult<T> result, out T value)
    {
        if (result.IsSuccess)
        {
            value = result.Value!;
            return true;
        }

        SetFailure(result.Failures);
        value = default!;
        return false;
    }

    private void ClearFailure()
    {
        ErrorMessage = string.Empty;
        lastFailureCodes = [];
    }

    private void SetFailure(params LlmChatUiFailure[] failures)
        => SetFailure((IReadOnlyList<LlmChatUiFailure>)failures);

    private void SetFailure(IReadOnlyList<LlmChatUiFailure> failures)
    {
        ErrorMessage = string.Join(' ', failures.Select(failure => failure.Message));
        lastFailureCodes = failures.Select(failure => failure.Code).Distinct(StringComparer.Ordinal).ToArray();
    }

    private void SetUnexpectedFailure()
        => SetFailure(new LlmChatUiFailure(
            LlmChatUiFailureCodes.RequestFailed,
            "The Simple Chat request could not be completed."));

}

internal sealed record LlmChatPendingTurn(Guid OperationId, string Message, DateTimeOffset AdmittedAtUtc);

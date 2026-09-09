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
    TimeProvider timeProvider) : IDisposable {
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

    private CancellationTokenSource? authorizationRead;
    private CancellationTokenSource? conversationRead;
    private CancellationTokenSource? definitionRead;
    private CancellationTokenSource? transcriptRead;
    private CancellationTokenSource? operationRead;
    private CancellationTokenSource? mutation;
    private bool initialized;
    private bool desiredObserved;
    private bool routeOwned;
    private bool chooseDefault;
    private bool disposed;

    public Guid? DesiredConversationId { get; private set; }
    public long SelectionGeneration { get; private set; }
    public bool IsAuthorizing => !initialized || authorizationRead is not null;
    public bool IsLoading => authorizationRead is not null || conversationRead is not null || definitionRead is not null || transcriptRead is not null;

    public bool IsMutating => mutation is not null;

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
        ActiveOperation is {
            Status: Operations.LlmChatOperationStatus.RecoveryRequired
        } operation &&
        SelectedConversation?.ActiveOperationId == operation.OperationId;

    public string OperationStatusText => operationState.StatusText;

    private IReadOnlyList<string> lastFailureCodes = [];

    public async Task InitializeAsync(CancellationToken cancellationToken, Guid? preferredConversationId = null) {
        if (disposed) {
            return;
        }
        if (!desiredObserved) {
            ObserveDesired(preferredConversationId, fromRoute: preferredConversationId.HasValue, allowDefault: preferredConversationId is null);
        }
        using var request = Begin(ref authorizationRead, cancellationToken);
        try {
            var access = await authorization.GetAsync(request.Token);
            if (!Owns(authorizationRead, request)) {
                return;
            }
            Authorization = access;
            if (!access.CanRead) {
                return;
            }
            if (access.CanManage) {
                await LoadDefinitionsAsync(false, request.Token);
            }
            if (!Owns(authorizationRead, request)) {
                return;
            }
            await LoadConversationsAsync(false, request.Token);
            if (!Owns(authorizationRead, request)) {
                return;
            }
            if (chooseDefault && DesiredConversationId is null && Conversations.Count > 0) {
                ObserveDesired(Conversations[0].ConversationId, fromRoute: false, allowDefault: false);
            }
            initialized = true;
            if (DesiredConversationId is { } target) {
                await LoadTranscriptAsync(target, false, request.Token);
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (Owns(authorizationRead, request)) {
                LogReadFailure(exception, "authorization");
                SetUnexpectedFailure();
            }
        } finally {
            if (ReferenceEquals(authorizationRead, request)) {
                authorizationRead = null;
                initialized = true;
            }
        }
    }

    public Task<bool> SetRequestedConversationAsync(Guid? conversationId, CancellationToken cancellationToken) {
        if (disposed) {
            return Task.FromResult(false);
        }
        var normalized = conversationId == Guid.Empty ? null : conversationId;
        var sameAccepted = DesiredConversationId == normalized && SelectedConversation?.ConversationId == normalized;
        if (!sameAccepted || !desiredObserved) {
            ObserveDesired(normalized, fromRoute: true, allowDefault: false);
        } else {
            routeOwned = true;
        }
        return !initialized || !Authorization.CanRead ? Task.FromResult(false)
            : normalized is { } id && !sameAccepted ? LoadTranscriptAsync(id, false, cancellationToken) : Task.FromResult(true);
    }

    private void ObserveDesired(Guid? id, bool fromRoute, bool allowDefault) {
        desiredObserved = true;
        DesiredConversationId = id == Guid.Empty ? null : id;
        routeOwned = fromRoute;
        chooseDefault = allowDefault;
        SelectionGeneration++;
        Stop(ref transcriptRead);
        Stop(ref operationRead);
        Stop(ref mutation);
        SelectedConversation = null;
        transcriptPage.Clear();
        operationState.Reset();
        ClearFailure();
    }

    public bool IsCurrentSelection(long generation) => !disposed && generation == SelectionGeneration;

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

    public Task<bool> SelectConversationAsync(Guid conversationId, CancellationToken cancellationToken) {
        if (disposed || !Authorization.CanRead || conversationId == Guid.Empty) {
            return Task.FromResult(false);
        }
        if (DesiredConversationId == conversationId && SelectedConversation?.ConversationId == conversationId) {
            return Task.FromResult(true);
        }
        ObserveDesired(conversationId, fromRoute: false, allowDefault: false);
        return LoadTranscriptAsync(conversationId, false, cancellationToken);
    }

    public Task<bool> ReloadSelectedAsync(CancellationToken cancellationToken)
        => SelectedConversation is null
            ? Task.FromResult(false)
            : LoadTranscriptAsync(SelectedConversation.ConversationId, append: false, cancellationToken);

    public async Task<Guid?> PrepareActiveOperationAsync(CancellationToken cancellationToken) {
        if (disposed) {
            return null;
        }
        operationState.Prepare();
        if (SelectedConversation?.ActiveOperationId is not { } operationId) {
            operationState.Restore(null);
            return null;
        }
        var generation = SelectionGeneration;
        using var request = Begin(ref operationRead, cancellationToken);
        try {
            var result = await operations.GetAsync(operationId, request.Token);
            if (!Owns(operationRead, request) || !IsCurrentSelection(generation)
                || !TryGetValue(result, out var operation) || !HasExpectedIdentity(operation, operationId)) {
                return null;
            }
            operationState.Restore(operation);
            return operationId;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (Owns(operationRead, request) && IsCurrentSelection(generation)) {
                LogReadFailure(exception, "operation restoration");
                SetUnexpectedFailure();
            }
            return null;
        } finally {
            End(ref operationRead, request);
        }
    }

    public void ApplyOperationProjection(LlmChatOperationProjectionState projection) {
        operationState.ApplyProjection(projection);
    }

    public async Task<bool> RefreshActiveOperationAsync(Guid operationId, CancellationToken cancellationToken) {
        if (disposed || SelectedConversation is not { } selected) {
            return false;
        }
        var generation = SelectionGeneration;
        using var request = Begin(ref operationRead, cancellationToken);
        try {
            var result = await operations.GetAsync(operationId, request.Token);
            if (!Owns(operationRead, request) || !IsCurrentSelection(generation)
                || !TryGetValue(result, out var operation) || !HasExpectedIdentity(operation, operationId)) {
                return false;
            }
            if (!await LoadTranscriptAsync(selected.ConversationId, false, request.Token)
                || !Owns(operationRead, request) || !IsCurrentSelection(generation)) {
                return false;
            }
            operationState.CompleteRefresh(operation);
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (Owns(operationRead, request) && IsCurrentSelection(generation)) {
                LogReadFailure(exception, "operation refresh");
                SetUnexpectedFailure();
            }
            return false;
        } finally {
            End(ref operationRead, request);
        }
    }

    public async Task<Guid?> ReloadForProfileChangeAsync(CancellationToken cancellationToken) {
        Stop(ref authorizationRead);
        Stop(ref conversationRead);
        Stop(ref definitionRead);
        var requested = routeOwned ? DesiredConversationId : null;
        ObserveDesired(requested, routeOwned, allowDefault: !routeOwned && requested is null);
        conversationPage.Clear();
        definitionPage.Clear();
        initialized = false;
        await InitializeAsync(cancellationToken, requested);
        return await PrepareActiveOperationAsync(cancellationToken);
    }

    public void SetFollowerFailure(IReadOnlyList<LlmChatUiFailure> failures)
        => SetGatewayFailure(failures);

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
        CancellationToken cancellationToken) {
        if (!Authorization.CanManage ||
            !ActiveDefinitions.Any(item =>
                item.DefinitionId == definitionId && item.Status == LlmChatDefinitionStatus.Active)) {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Choose an active Simple Chat definition."));
            return false;
        }

        return await RunMutationAsync(
            "create conversation",
            async token => {
                var result = await conversations.CreateAsync(definitionId, title.Trim(), token);
                if (token.IsCancellationRequested || !TryGetValue(result, out var view)) {
                    return false;
                }

                ReplaceOrInsertConversation(view.Conversation);
                SelectView(view);
                return true;
            }, cancellationToken);
    }

    public async Task<bool> RenameSelectedAsync(string title, CancellationToken cancellationToken) {
        if (!Authorization.CanManage || SelectedConversation is not { } selected) {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Select a conversation you can manage."));
            return false;
        }

        return await RunMutationAsync(
            "rename conversation",
            async token => {
                var result = await conversations.RenameAsync(
                    selected.ConversationId,
                    title.Trim(),
                    selected.ConcurrencyToken,
                    selected.TranscriptRevision,
                    token);
                if (token.IsCancellationRequested || !TryGetValue(result, out var view)) {
                    return false;
                }

                SelectedConversation = view.Conversation;
                ReplaceOrInsertConversation(view.Conversation);
                return true;
            }, cancellationToken);
    }

    public async Task<bool> ArchiveSelectedAsync(CancellationToken cancellationToken) {
        if (!Authorization.CanManage || SelectedConversation is not { } selected) {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.Forbidden,
                "Select a conversation you can manage."));
            return false;
        }

        return await RunMutationAsync(
            "archive conversation",
            async token => {
                var result = await conversations.ArchiveAsync(
                    selected.ConversationId,
                    selected.ConcurrencyToken,
                    token);
                if (token.IsCancellationRequested || !TryGetValue(result, out var view)) {
                    return false;
                }

                SelectedConversation = view.Conversation;
                ReplaceOrInsertConversation(view.Conversation);
                return true;
            }, cancellationToken);
    }

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken) {
        var normalizedMessage = message.Trim();
        if (!Authorization.CanExecute ||
            SelectedConversation is not { Status: LlmChatConversationStatus.Active } selected ||
            selected.ActiveOperationId.HasValue ||
            string.IsNullOrWhiteSpace(normalizedMessage)) {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.InvalidInput,
                "Select an active conversation and enter a message."));
            return false;
        }

        var operationId = operationState.GetAdmissionOperationId(selected.ConversationId, normalizedMessage);
        return await RunMutationAsync(
            "admit conversation turn",
            async token => {
                var result = await operations.SendAsync(
                    operationId,
                    selected.ConversationId,
                    selected.TranscriptRevision,
                    normalizedMessage,
                    token);
                if (token.IsCancellationRequested || !TryGetValue(result, out var operation)) {
                    return false;
                }

                if (operation.OperationId != operationId ||
                    operation.ConversationId != selected.ConversationId) {
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
            }, cancellationToken);
    }

    private async Task<bool> LoadConversationsAsync(bool append, CancellationToken cancellationToken) {
        if (disposed || !Authorization.CanRead || append && conversationRead is not null) {
            return false;
        }
        using var request = Begin(ref conversationRead, cancellationToken);
        ClearFailure();
        try {
            var result = await conversations.ListPageAsync(new(take: ConversationPageSize,
                cursor: append ? conversationPage.NextCursor : null), request.Token);
            if (!Owns(conversationRead, request) || !TryGetValue(result, out var page)) {
                return false;
            }
            if (append) {
                conversationPage.Append(page.Items, page.NextCursor);
            } else {
                conversationPage.Replace(page.Items, page.NextCursor);
            }
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (Owns(conversationRead, request)) {
                LogReadFailure(exception, "conversation list");
                SetUnexpectedFailure();
            }
            return false;
        } finally {
            End(ref conversationRead, request);
        }
    }

    private async Task<bool> LoadDefinitionsAsync(bool append, CancellationToken cancellationToken) {
        if (disposed || !Authorization.CanManage || append && definitionRead is not null) {
            return false;
        }
        using var request = Begin(ref definitionRead, cancellationToken);
        ClearFailure();
        try {
            var result = await definitions.ListPageAsync(new(take: DefinitionPageSize, status: LlmChatDefinitionStatus.Active,
                cursor: append ? definitionPage.NextCursor : null), request.Token);
            if (!Owns(definitionRead, request) || !TryGetValue(result, out var page)) {
                return false;
            }
            var items = page.Items.Where(item => item.Status == LlmChatDefinitionStatus.Active)
                .Select(item => item with { Tags = System.Collections.Immutable.ImmutableArray.CreateRange(item.Tags) });
            if (append) {
                definitionPage.Append(items, page.NextCursor);
            } else {
                definitionPage.Replace(items, page.NextCursor);
            }
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (Owns(definitionRead, request)) {
                LogReadFailure(exception, "definition list");
                SetUnexpectedFailure();
            }
            return false;
        } finally {
            End(ref definitionRead, request);
        }
    }

    private async Task<bool> LoadTranscriptAsync(Guid conversationId, bool append, CancellationToken cancellationToken) {
        if (disposed || !Authorization.CanRead || DesiredConversationId != conversationId || append && transcriptRead is not null) {
            return false;
        }
        var generation = SelectionGeneration;
        using var request = Begin(ref transcriptRead, cancellationToken);
        ClearFailure();
        try {
            var result = await conversations.GetAsync(conversationId,
                new(TranscriptPageSize, append ? transcriptPage.NextCursor : null), request.Token);
            if (!Owns(transcriptRead, request) || !IsCurrentSelection(generation) || !TryGetValue(result, out var view)) {
                return false;
            }
            if (view.Conversation.ConversationId != conversationId) {
                logger.LogError("Simple Chat transcript returned mismatched conversation identity for {ConversationId}", conversationId);
                SetUnexpectedFailure();
                return false;
            }
            if (!append) {
                transcriptPage.Clear();
                operationState.Reset();
            }
            transcriptPage.Append(view.Messages.Where(message => message.Role is LlmMessageRole.User or LlmMessageRole.Assistant), view.NextMessageCursor);
            SelectedConversation = view.Conversation;
            ReplaceOrInsertConversation(view.Conversation);
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (Owns(transcriptRead, request) && IsCurrentSelection(generation)) {
                LogReadFailure(exception, "transcript");
                SetUnexpectedFailure();
            }
            return false;
        } finally {
            End(ref transcriptRead, request);
        }
    }

    private async Task<bool> RunMutationAsync(string operationName, Func<CancellationToken, Task<bool>> action, CancellationToken cancellationToken) {
        if (disposed || mutation is not null) {
            return false;
        }
        var generation = SelectionGeneration;
        using var request = Begin(ref mutation, cancellationToken);
        ClearFailure();
        try {
            return await action(request.Token) && Owns(mutation, request) && IsCurrentSelection(generation);
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (Owns(mutation, request) && IsCurrentSelection(generation)) {
                LogReadFailure(exception, operationName);
                SetUnexpectedFailure();
            }
            return false;
        } finally {
            End(ref mutation, request);
        }
    }

    private void SelectView(LlmChatConversationView view) {
        DesiredConversationId = view.Conversation.ConversationId;
        routeOwned = false;
        chooseDefault = false;
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
        CancellationToken cancellationToken) {
        if (!isAllowed || ActiveOperation is not { } current) {
            SetFailure(new LlmChatUiFailure(
                LlmChatUiFailureCodes.InvalidInput,
                "The requested Simple Chat recovery action is not available."));
            return false;
        }

        return await RunMutationAsync(
            operationName,
            async token => {
                var result = await mutate(current, token);
                if (token.IsCancellationRequested || !TryGetValue(result, out var operation) ||
                    !HasExpectedIdentity(operation, current.OperationId)) {
                    return false;
                }

                operationState.Restore(operation);
                if (refreshTranscript && SelectedConversation is { } selected) {
                    var refreshed = await LoadTranscriptAsync(
                        selected.ConversationId,
                        append: false,
                        token);
                    if (!refreshed || token.IsCancellationRequested) {
                        return false;
                    }
                }

                operationState.CompleteMutation(operation, confirmRecoveryEvidence);

                return true;
            }, cancellationToken);
    }

    private bool HasExpectedIdentity(LlmChatOperationView operation, Guid operationId) {
        if (SelectedConversation is { } selected &&
            operation.OperationId == operationId &&
            operation.ConversationId == selected.ConversationId) {
            return true;
        }

        logger.LogError(
            "Simple Chat operation returned mismatched identity. ConversationId={ConversationId} OperationId={OperationId}.",
            SelectedConversation?.ConversationId,
            operationId);
        SetUnexpectedFailure();
        return false;
    }

    private void ReplaceOrInsertConversation(LlmChatConversationListItem conversation) {
        conversationPage.UpsertFirst(conversation);
    }

    private bool TryGetValue<T>(LlmChatUiResult<T> result, out T value) {
        if (result.IsSuccess) {
            value = result.Value!;
            return true;
        }

        SetGatewayFailure(result.Failures);
        value = default!;
        return false;
    }

    private void ClearFailure() {
        ErrorMessage = string.Empty;
        lastFailureCodes = [];
    }

    private void SetGatewayFailure(IReadOnlyList<LlmChatUiFailure> failures) {
        SetFailure(failures.Select(failure => failure.Code switch {
            LlmChatUiFailureCodes.Forbidden => new LlmChatUiFailure(failure.Code, "You are not authorized to perform this Simple Chat action."),
            LlmChatUiFailureCodes.InvalidInput => new LlmChatUiFailure(failure.Code, "Review the Simple Chat values and try again."),
            _ => LlmChatUiResultMapper.FromFailureCode(failure.Code)
        }).ToArray());
    }

    private void SetFailure(params LlmChatUiFailure[] failures)
        => SetFailure((IReadOnlyList<LlmChatUiFailure>)failures);

    private void SetFailure(IReadOnlyList<LlmChatUiFailure> failures) {
        ErrorMessage = string.Join(' ', failures.Select(failure => failure.Message));
        lastFailureCodes = failures.Select(failure => failure.Code).Distinct(StringComparer.Ordinal).ToArray();
    }

    private void SetUnexpectedFailure()
        => SetFailure(new LlmChatUiFailure(
            LlmChatUiFailureCodes.RequestFailed,
            "The Simple Chat request could not be completed."));

    public void Dispose() {
        disposed = true;
        SelectionGeneration++;
        Stop(ref authorizationRead);
        Stop(ref conversationRead);
        Stop(ref definitionRead);
        Stop(ref transcriptRead);
        Stop(ref operationRead);
        Stop(ref mutation);
    }
    private bool Owns(CancellationTokenSource? owner, CancellationTokenSource request)
        => !disposed && ReferenceEquals(owner, request) && !request.IsCancellationRequested;
    private static CancellationTokenSource Begin(ref CancellationTokenSource? owner, CancellationToken token) {
        Stop(ref owner);
        return owner = CancellationTokenSource.CreateLinkedTokenSource(token);
    }
    private static void End(ref CancellationTokenSource? owner, CancellationTokenSource request) {
        if (ReferenceEquals(owner, request)) {
            owner = null;
        }
    }
    private static void Stop(ref CancellationTokenSource? owner) {
        var previous = owner;
        owner = null;
        previous?.Cancel();
    }
    private void LogReadFailure(Exception exception, string operation)
        => logger.LogWarning("Simple Chat {Operation} failed for selection {Generation} with {ExceptionType}", operation, SelectionGeneration, exception.GetType().Name);

}

internal sealed record LlmChatPendingTurn(Guid OperationId, string Message, DateTimeOffset AdmittedAtUtc);

using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// Options bounding the outbound context window handed to the selection policy. The transcript itself
/// is never bounded by these values — they shape only what a single invocation sends.
/// </summary>
public sealed record LlmConversationServiceOptions
{
    public int MaximumContextWindowMessages { get; init; } = LlmInvocationRequest.MaximumMessages;

    public int MaximumContextWindowCharacters { get; init; } = 600_000;
}

/// <summary>
/// Ordinary multi-turn LLM conversation application service above the stateless invocation port.
/// The persisted application transcript is canonical; provider conversation state is at most an opaque
/// acceleration envelope. A turn is atomic: it is admitted through a revision compare-and-swap that
/// appends the pending user entry together with an in-flight marker, then either completes with the
/// assistant entry or rolls the transcript back to its pre-turn content. No agents, tools, memory,
/// capability composition, workspace authority, approvals, or process semantics participate.
/// </summary>
public sealed class LlmConversationService : ILlmConversationService
{
    private const int MaximumCompensationAttempts = 3;

    private readonly ILlmInvocationPort _invocationPort;
    private readonly ILlmConversationStore _store;
    private readonly ILlmConversationTurnStore _turnStore;
    private readonly ILlmConversationContextWindowPolicy _contextWindowPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly LlmConversationServiceOptions _options;

    public LlmConversationService(
        ILlmInvocationPort invocationPort,
        ILlmConversationStore store,
        ILlmConversationContextWindowPolicy contextWindowPolicy,
        TimeProvider timeProvider,
        LlmConversationServiceOptions? options = null)
        : this(
            invocationPort,
            store,
            new DocumentLlmConversationTurnStore(store),
            contextWindowPolicy,
            timeProvider,
            options)
    {
    }

    public LlmConversationService(
        ILlmInvocationPort invocationPort,
        ILlmConversationStore store,
        ILlmConversationTurnStore turnStore,
        ILlmConversationContextWindowPolicy contextWindowPolicy,
        TimeProvider timeProvider,
        LlmConversationServiceOptions? options = null)
    {
        _invocationPort = invocationPort ?? throw new ArgumentNullException(nameof(invocationPort));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
        _contextWindowPolicy = contextWindowPolicy ?? throw new ArgumentNullException(nameof(contextWindowPolicy));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? new LlmConversationServiceOptions();
    }

    public async Task<LlmConversationDocument> StartAsync(
        LlmConversationStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _timeProvider.GetUtcNow();
        var snapshot = LlmConversationProviderSnapshot.FromProfile(request.Provider, request.Model);
        var entries = ImmutableArray<LlmConversationTranscriptEntry>.Empty;
        var revision = 0L;
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            entries = entries.Add(new LlmConversationTranscriptEntry(
                Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.System, request.SystemPrompt, now));
            revision = 1;
        }

        var document = new LlmConversationDocument(
            request.ConversationId ?? Guid.NewGuid(),
            request.Title,
            snapshot,
            now,
            now,
            revision,
            entries);
        return await _store.CreateAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public Task<LlmConversationDocument?> TryGetAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => _store.TryGetAsync(conversationId, cancellationToken);

    public async Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. summaries.OrderByDescending(summary => summary.UpdatedAtUtc)];
    }

    public async Task<LlmConversationDocument> RenameAsync(
        Guid conversationId, string title, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        var document = await RequireAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (document.ActiveTurn is not null)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.TurnAlreadyActive,
                document.ConversationId,
                $"Active turn: {document.ActiveTurn.TurnId:N}.");
        }

        if (document.TranscriptRevision != expectedTranscriptRevision)
        {
            throw new LlmConversationException(LlmConversationFailureKind.RevisionConflict, conversationId);
        }

        var renamed = new LlmConversationDocument(
            document.ConversationId,
            title,
            document.Provider,
            document.CreatedAtUtc,
            _timeProvider.GetUtcNow(),
            document.TranscriptRevision + 1,
            document.Entries,
            document.ActiveTurn,
            document.AccelerationState);
        return await _store.ReplaceAsync(renamed, document.TranscriptRevision, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationTurnResult> SendAsync(
        LlmConversationTurnRequest request, CancellationToken cancellationToken = default)
    {
        var admission = await AdmitTurnAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var invocationResult = await _invocationPort.InvokeAsync(admission.InvocationRequest, cancellationToken)
                .ConfigureAwait(false);
            return await CompleteTurnAsync(admission, invocationResult, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CompensateAdmittedTurnAsync(admission.Conversation).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmConversationTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await RequireTurnAsync(request.ConversationId, cancellationToken).ConfigureAwait(false);
        if (current.ActiveTurn is not null)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.TurnAlreadyActive,
                current.ConversationId,
                $"Active turn: {current.ActiveTurn.TurnId:N}.");
        }

        if (current.TranscriptRevision != request.ExpectedTranscriptRevision)
        {
            throw new LlmConversationException(LlmConversationFailureKind.RevisionConflict, current.ConversationId);
        }

        if (current.EntryCount > LlmConversationDocument.MaximumEntries - 2)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                current.ConversationId,
                "A complete turn requires capacity for both user and assistant transcript entries.");
        }

        var snapshot = ResolveTurnSnapshot(current, request);
        var admittedAt = _timeProvider.GetUtcNow();
        var providerChanged = snapshot != current.Provider;
        var pendingEntry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), request.TurnId ?? Guid.NewGuid(), LlmMessageRole.User, request.UserText, admittedAt);
        var activeTurn = new LlmConversationActiveTurn(
            pendingEntry.TurnId,
            pendingEntry.EntryId,
            admittedAt,
            current.TranscriptRevision + 1,
            providerChanged
                ? new LlmConversationTurnCompensation(current.Provider, current.AccelerationState)
                : null);
        var admitted = await _turnStore.AdmitAsync(
            new LlmConversationTurnAdmissionWrite(
                current,
                snapshot,
                pendingEntry,
                activeTurn,
                providerChanged ? null : current.AccelerationState,
                admittedAt,
                _options.MaximumContextWindowMessages),
            cancellationToken)
            .ConfigureAwait(false);
        var document = ToDocument(admitted);
        var storedPendingEntry = document.Entries[^1];
        IReadOnlyList<LlmMessage> window;
        try
        {
            window = BuildContextWindow(document, storedPendingEntry);
        }
        catch
        {
            await CompensateTurnAsync(
                document.ConversationId,
                storedPendingEntry.TurnId,
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new LlmConversationTurnAdmission(
            document,
            storedPendingEntry,
            new LlmInvocationRequest(
                request.Provider,
                snapshot.Model,
                window,
                responseFormat: request.ResponseFormat,
                settings: request.Settings,
                timeout: request.Timeout,
                correlationId: request.CorrelationId),
            admitted.EntryCount);
    }

    public async Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmConversationAdmittedTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await RequireTurnAsync(request.ConversationId, cancellationToken).ConfigureAwait(false);
        var activeTurn = current.ActiveTurn;
        if (activeTurn?.TurnId != request.TurnId)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.TurnNotActive,
                request.ConversationId);
        }

        var pendingEntry = current.ContextEntries.SingleOrDefault(entry =>
            entry.EntryId == activeTurn.PendingUserEntryId &&
            entry.TurnId == activeTurn.TurnId &&
            entry.Role == LlmMessageRole.User);
        if (pendingEntry is null || !current.Provider.Matches(request.Provider, request.Model))
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                request.ConversationId,
                "The admitted turn no longer matches its pending user entry or provider snapshot.");
        }

        var document = ToDocument(current);
        var window = BuildContextWindow(document, pendingEntry);
        return new LlmConversationTurnAdmission(
            document,
            pendingEntry,
            new LlmInvocationRequest(
                request.Provider,
                current.Provider.Model,
                window,
                responseFormat: request.ResponseFormat,
                settings: request.Settings,
                timeout: request.Timeout,
                correlationId: request.CorrelationId),
            current.EntryCount);
    }

    public async Task<LlmConversationTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(invocationResult);
        var admitted = admission.Conversation;
        if (admitted.ActiveTurn?.PendingUserEntryId != admission.UserEntry.EntryId ||
            admitted.ActiveTurn.TurnId != admission.UserEntry.TurnId)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                admitted.ConversationId,
                "The turn admission no longer identifies its pending user entry.");
        }

        var completedAt = _timeProvider.GetUtcNow();
        var assistantEntry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(),
            admission.UserEntry.TurnId,
            LlmMessageRole.Assistant,
            invocationResult.ResponseText,
            completedAt,
            invocationResult.Model,
            invocationResult.Usage);
        var stored = await _turnStore.CompleteAsync(
            new LlmConversationTurnCompletionWrite(
                admitted.ConversationId,
                admission.UserEntry.TurnId,
                admission.UserEntry.EntryId,
                admitted.TranscriptRevision,
                admission.PersistedEntryCount > 0
                    ? admission.PersistedEntryCount
                    : admitted.Entries.Length,
                assistantEntry,
                completedAt,
                _options.MaximumContextWindowMessages),
            cancellationToken)
            .ConfigureAwait(false);
        var storedDocument = ToDocument(stored);
        var storedAssistantEntry = storedDocument.Entries[^1];
        if (storedAssistantEntry.EntryId != assistantEntry.EntryId)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                storedDocument.ConversationId,
                "The committed assistant transcript entry could not be reloaded.");
        }

        return new LlmConversationTurnResult(storedDocument, admission.UserEntry, storedAssistantEntry);
    }

    public async Task<LlmConversationDocument> CompensateTurnAsync(
        Guid conversationId,
        Guid turnId,
        CancellationToken cancellationToken = default)
    {
        var recovered = await _turnStore.CompensateAsync(
            conversationId,
            turnId,
            _timeProvider.GetUtcNow(),
            _options.MaximumContextWindowMessages,
            cancellationToken).ConfigureAwait(false);
        return ToDocument(recovered);
    }

    public async Task<LlmConversationDocument> AbandonActiveTurnAsync(
        Guid conversationId, Guid turnId, CancellationToken cancellationToken = default)
        => await CompensateTurnAsync(conversationId, turnId, cancellationToken).ConfigureAwait(false);

    public async Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => await _store.DeleteAsync(conversationId, cancellationToken).ConfigureAwait(false);

    private async Task<LlmConversationDocument> RequireAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var document = await _store.TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return document
               ?? throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
    }

    private static LlmConversationProviderSnapshot ResolveTurnSnapshot(
        LlmConversationTurnSnapshot document, LlmConversationTurnRequest request)
    {
        if (document.Provider.Matches(request.Provider, request.Model))
        {
            return document.Provider;
        }

        if (request.ProviderChangePolicy != LlmConversationProviderChangePolicy.Adopt)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.ProviderModelMismatch,
                document.ConversationId,
                $"Snapshot provider '{document.Provider.ProviderId:N}' model '{document.Provider.Model}'.");
        }

        return LlmConversationProviderSnapshot.FromProfile(request.Provider, request.Model);
    }

    private IReadOnlyList<LlmMessage> BuildContextWindow(
        LlmConversationDocument admitted, LlmConversationTranscriptEntry pendingEntry)
    {
        var window = _contextWindowPolicy.SelectWindow(new LlmConversationContextWindowRequest(
            admitted.Entries,
            _options.MaximumContextWindowMessages,
            _options.MaximumContextWindowCharacters));
        if (window is not { Count: > 0 }
            || window[^1].Role != LlmMessageRole.User
            || !string.Equals(window[^1].Text, pendingEntry.Text, StringComparison.Ordinal))
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                admitted.ConversationId,
                "The context window policy must retain the pending user message as the final window entry.");
        }

        return window;
    }

    private async Task CompensateAdmittedTurnAsync(LlmConversationDocument admitted)
    {
        for (var attempt = 1; attempt <= MaximumCompensationAttempts; attempt++)
        {
            try
            {
                await CompensateTurnAsync(
                        admitted.ConversationId,
                        admitted.ActiveTurn!.TurnId,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                return;
            }
            catch (LlmConversationException exception)
                when (exception.Kind is LlmConversationFailureKind.TurnNotActive or
                      LlmConversationFailureKind.NotFound)
            {
                return;
            }
            catch (LlmConversationException exception)
                when (exception.Kind == LlmConversationFailureKind.ConcurrencyConflict
                      && attempt < MaximumCompensationAttempts)
            {
            }
        }
    }

    private async Task<LlmConversationTurnSnapshot> RequireTurnAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
        => await _turnStore.TryGetAsync(
               conversationId,
               _options.MaximumContextWindowMessages,
               cancellationToken).ConfigureAwait(false)
           ?? throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);

    private static LlmConversationDocument ToDocument(LlmConversationTurnSnapshot snapshot)
        => new(
            snapshot.ConversationId,
            snapshot.Title,
            snapshot.Provider,
            snapshot.CreatedAtUtc,
            snapshot.UpdatedAtUtc,
            snapshot.TranscriptRevision,
            snapshot.ContextEntries,
            snapshot.ActiveTurn,
            snapshot.AccelerationState);
}

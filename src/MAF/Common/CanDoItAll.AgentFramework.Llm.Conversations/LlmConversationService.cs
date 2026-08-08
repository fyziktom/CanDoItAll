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
    private readonly ILlmConversationContextWindowPolicy _contextWindowPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly LlmConversationServiceOptions _options;

    public LlmConversationService(
        ILlmInvocationPort invocationPort,
        ILlmConversationStore store,
        ILlmConversationContextWindowPolicy contextWindowPolicy,
        TimeProvider timeProvider,
        LlmConversationServiceOptions? options = null)
    {
        _invocationPort = invocationPort ?? throw new ArgumentNullException(nameof(invocationPort));
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
            Guid.NewGuid(),
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
        ArgumentNullException.ThrowIfNull(request);
        var document = await RequireAsync(request.ConversationId, cancellationToken).ConfigureAwait(false);
        if (document.ActiveTurn is not null)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.TurnAlreadyActive,
                document.ConversationId,
                $"Active turn: {document.ActiveTurn.TurnId:N}.");
        }

        if (document.TranscriptRevision != request.ExpectedTranscriptRevision)
        {
            throw new LlmConversationException(LlmConversationFailureKind.RevisionConflict, document.ConversationId);
        }

        if (document.Entries.Length > LlmConversationDocument.MaximumEntries - 2)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                document.ConversationId,
                "A complete turn requires capacity for both user and assistant transcript entries.");
        }

        var snapshot = ResolveTurnSnapshot(document, request);
        var admitted = await AdmitTurnAsync(document, request, snapshot, cancellationToken).ConfigureAwait(false);
        var pendingEntry = admitted.Entries[^1];
        try
        {
            var window = BuildContextWindow(admitted, pendingEntry);
            var invocationResult = await _invocationPort.InvokeAsync(
                new LlmInvocationRequest(
                    request.Provider,
                    snapshot.Model,
                    window,
                    responseFormat: request.ResponseFormat,
                    settings: request.Settings,
                    timeout: request.Timeout,
                    correlationId: request.CorrelationId),
                cancellationToken).ConfigureAwait(false);

            var completedAt = _timeProvider.GetUtcNow();
            var assistantEntry = new LlmConversationTranscriptEntry(
                Guid.NewGuid(),
                pendingEntry.TurnId,
                LlmMessageRole.Assistant,
                invocationResult.ResponseText,
                completedAt,
                invocationResult.Model,
                invocationResult.Usage);
            var completed = new LlmConversationDocument(
                admitted.ConversationId,
                admitted.Title,
                admitted.Provider,
                admitted.CreatedAtUtc,
                completedAt,
                admitted.TranscriptRevision + 1,
                admitted.Entries.Add(assistantEntry),
                activeTurn: null,
                admitted.AccelerationState);
            var stored = await _store.ReplaceAsync(completed, admitted.TranscriptRevision, cancellationToken)
                .ConfigureAwait(false);
            return new LlmConversationTurnResult(stored, pendingEntry, assistantEntry);
        }
        catch
        {
            await CompensateAdmittedTurnAsync(admitted).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LlmConversationDocument> AbandonActiveTurnAsync(
        Guid conversationId, Guid turnId, CancellationToken cancellationToken = default)
    {
        var document = await RequireAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (document.ActiveTurn is null || document.ActiveTurn.TurnId != turnId)
        {
            throw new LlmConversationException(LlmConversationFailureKind.TurnNotActive, conversationId);
        }

        var recovered = BuildCompensatedDocument(document);
        return await _store.ReplaceAsync(recovered, document.TranscriptRevision, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => await _store.DeleteAsync(conversationId, cancellationToken).ConfigureAwait(false);

    private async Task<LlmConversationDocument> RequireAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var document = await _store.TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return document
               ?? throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
    }

    private static LlmConversationProviderSnapshot ResolveTurnSnapshot(
        LlmConversationDocument document, LlmConversationTurnRequest request)
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

    private async Task<LlmConversationDocument> AdmitTurnAsync(
        LlmConversationDocument document,
        LlmConversationTurnRequest request,
        LlmConversationProviderSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var admittedAt = _timeProvider.GetUtcNow();
        var providerChanged = !ReferenceEquals(snapshot, document.Provider) && snapshot != document.Provider;
        var admittedRevision = document.TranscriptRevision + 1;
        var pendingEntry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.User, request.UserText, admittedAt);
        var admitted = new LlmConversationDocument(
            document.ConversationId,
            document.Title,
            snapshot,
            document.CreatedAtUtc,
            admittedAt,
            admittedRevision,
            document.Entries.Add(pendingEntry),
            new LlmConversationActiveTurn(
                pendingEntry.TurnId,
                pendingEntry.EntryId,
                admittedAt,
                admittedRevision,
                providerChanged
                    ? new LlmConversationTurnCompensation(document.Provider, document.AccelerationState)
                    : null),
            providerChanged ? null : document.AccelerationState);
        return await _store.ReplaceAsync(admitted, document.TranscriptRevision, cancellationToken).ConfigureAwait(false);
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
            var current = await _store.TryGetAsync(admitted.ConversationId, CancellationToken.None)
                .ConfigureAwait(false);
            if (current?.ActiveTurn is null || current.ActiveTurn.TurnId != admitted.ActiveTurn!.TurnId)
            {
                return;
            }

            var compensated = BuildCompensatedDocument(current);
            try
            {
                await _store.ReplaceAsync(compensated, current.TranscriptRevision, CancellationToken.None)
                    .ConfigureAwait(false);
                return;
            }
            catch (LlmConversationException exception)
                when (exception.Kind == LlmConversationFailureKind.ConcurrencyConflict
                      && attempt < MaximumCompensationAttempts)
            {
            }
        }
    }

    private LlmConversationDocument BuildCompensatedDocument(LlmConversationDocument document)
    {
        var activeTurn = document.ActiveTurn
                         ?? throw new LlmConversationException(
                             LlmConversationFailureKind.TurnNotActive, document.ConversationId);
        var compensation = activeTurn.Compensation;
        return new LlmConversationDocument(
            document.ConversationId,
            document.Title,
            compensation?.Provider ?? document.Provider,
            document.CreatedAtUtc,
            _timeProvider.GetUtcNow(),
            document.TranscriptRevision + 1,
            [.. document.Entries.Where(entry => entry.EntryId != activeTurn.PendingUserEntryId)],
            activeTurn: null,
            compensation is null ? document.AccelerationState : compensation.AccelerationState);
    }
}

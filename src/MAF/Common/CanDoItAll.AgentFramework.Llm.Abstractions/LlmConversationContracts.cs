using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Llm.Abstractions;

/// <summary>
/// Immutable identity snapshot of the provider/model a conversation is bound to. Stores stable
/// identifiers only — never endpoint, credential, or transport configuration. The live
/// <see cref="ProviderProfile"/> is supplied per turn by the caller; equality against this snapshot is
/// decided by <see cref="Matches"/> on provider id, kind, and model (the recorded name is display
/// metadata and does not participate in matching, so renaming a provider profile does not invalidate
/// existing conversations).
/// </summary>
public sealed record LlmConversationProviderSnapshot
{
    public const int MaximumNameLength = 200;
    public const int MaximumModelLength = 200;

    public LlmConversationProviderSnapshot(Guid providerId, string providerName, ProviderKind providerKind, string model)
    {
        if (providerId == Guid.Empty)
        {
            throw new ArgumentException("A provider snapshot requires a non-empty provider id.", nameof(providerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, "Unknown provider kind.");
        }

        if (providerName.Trim().Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"A provider name cannot exceed {MaximumNameLength} characters.", nameof(providerName));
        }

        if (model.Trim().Length > MaximumModelLength)
        {
            throw new ArgumentException(
                $"A model name cannot exceed {MaximumModelLength} characters.", nameof(model));
        }

        ProviderId = providerId;
        ProviderName = providerName.Trim();
        ProviderKind = providerKind;
        Model = model.Trim();
    }

    public Guid ProviderId { get; }

    public string ProviderName { get; }

    public ProviderKind ProviderKind { get; }

    public string Model { get; }

    public static LlmConversationProviderSnapshot FromProfile(ProviderProfile provider, string model = "")
    {
        ArgumentNullException.ThrowIfNull(provider);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model;
        return new LlmConversationProviderSnapshot(provider.Id, provider.Name, provider.Kind, effectiveModel);
    }

    public bool Matches(ProviderProfile provider, string model)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? Model : model.Trim();
        return provider.Id == ProviderId
               && provider.Kind == ProviderKind
               && string.Equals(effectiveModel, Model, StringComparison.Ordinal);
    }
}

/// <summary>
/// A single canonical transcript record. The application transcript — not any provider-native
/// conversation object — is the source of truth for conversation content. Usage is recorded on
/// assistant entries only, exactly as reported by the stateless invocation port.
/// </summary>
public sealed record LlmConversationTranscriptEntry
{
    public const int MaximumTextLength = LlmMessage.MaximumTextLength;
    public const int MaximumModelLength = LlmConversationProviderSnapshot.MaximumModelLength;

    public LlmConversationTranscriptEntry(
        Guid entryId,
        Guid turnId,
        LlmMessageRole role,
        string text,
        DateTimeOffset createdAtUtc,
        string model = "",
        LlmUsage? usage = null)
    {
        if (entryId == Guid.Empty)
        {
            throw new ArgumentException("A transcript entry requires a non-empty entry id.", nameof(entryId));
        }

        if (turnId == Guid.Empty)
        {
            throw new ArgumentException("A transcript entry requires a non-empty turn id.", nameof(turnId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown transcript entry role.");
        }

        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"A transcript entry cannot exceed {MaximumTextLength} characters.", nameof(text));
        }

        var normalizedModel = model?.Trim() ?? string.Empty;
        if (normalizedModel.Length > MaximumModelLength)
        {
            throw new ArgumentException(
                $"A transcript entry model name cannot exceed {MaximumModelLength} characters.", nameof(model));
        }

        if (usage is not null && role != LlmMessageRole.Assistant)
        {
            throw new ArgumentException("Usage is recorded on assistant entries only.", nameof(usage));
        }

        EntryId = entryId;
        TurnId = turnId;
        Role = role;
        Text = text;
        CreatedAtUtc = createdAtUtc;
        Model = normalizedModel;
        Usage = usage;
    }

    public Guid EntryId { get; }

    /// <summary>Groups the user and assistant records that belong to the same logical turn.</summary>
    public Guid TurnId { get; }

    public LlmMessageRole Role { get; }

    public string Text { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>The model that produced an assistant entry; empty for user/system entries.</summary>
    public string Model { get; }

    /// <summary>Token usage for an assistant entry as reported by the invocation port; null otherwise.</summary>
    public LlmUsage? Usage { get; }
}

/// <summary>
/// Marker for a turn that has been admitted but not completed. While present, no other turn may be
/// admitted for the conversation. It survives a process crash and is cleared only by turn completion,
/// turn rollback, or an explicit <see cref="ILlmConversationService.AbandonActiveTurnAsync"/> decision —
/// never by a background heuristic.
/// </summary>
public sealed record LlmConversationActiveTurn
{
    public LlmConversationActiveTurn(
        Guid turnId,
        Guid pendingUserEntryId,
        DateTimeOffset admittedAtUtc,
        long admittedRevision,
        LlmConversationTurnCompensation? compensation = null)
    {
        if (turnId == Guid.Empty)
        {
            throw new ArgumentException("An active turn requires a non-empty turn id.", nameof(turnId));
        }

        if (pendingUserEntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "An active turn requires a non-empty pending user entry id.", nameof(pendingUserEntryId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(admittedRevision, 1);
        TurnId = turnId;
        PendingUserEntryId = pendingUserEntryId;
        AdmittedAtUtc = admittedAtUtc;
        AdmittedRevision = admittedRevision;
        Compensation = compensation;
    }

    public Guid TurnId { get; }

    public Guid PendingUserEntryId { get; }

    public DateTimeOffset AdmittedAtUtc { get; }

    public long AdmittedRevision { get; }

    public LlmConversationTurnCompensation? Compensation { get; }
}

public sealed record LlmConversationTurnCompensation
{
    public LlmConversationTurnCompensation(
        LlmConversationProviderSnapshot provider,
        LlmConversationAccelerationEnvelope? accelerationState)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        AccelerationState = accelerationState;
    }

    public LlmConversationProviderSnapshot Provider { get; }

    public LlmConversationAccelerationEnvelope? AccelerationState { get; }
}

/// <summary>
/// Optional opaque provider acceleration state (for example a provider-side conversation id). It is a
/// cache: dropping it at any time must never lose conversation content, because the application
/// transcript is canonical. It is invalidated automatically when the conversation adopts a different
/// provider or model. The payload is never interpreted by the conversation service.
/// </summary>
public sealed record LlmConversationAccelerationEnvelope
{
    public const int MaximumStrategyIdLength = 100;
    public const int MaximumPayloadLength = 256_000;

    public LlmConversationAccelerationEnvelope(string strategyId, string providerName, string model, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(payloadJson);
        if (strategyId.Trim().Length > MaximumStrategyIdLength)
        {
            throw new ArgumentException(
                $"An acceleration strategy id cannot exceed {MaximumStrategyIdLength} characters.", nameof(strategyId));
        }

        if (payloadJson.Length > MaximumPayloadLength)
        {
            throw new ArgumentException(
                $"An acceleration payload cannot exceed {MaximumPayloadLength} characters.", nameof(payloadJson));
        }

        StrategyId = strategyId.Trim();
        ProviderName = providerName.Trim();
        Model = model.Trim();
        PayloadJson = payloadJson;
    }

    public string StrategyId { get; }

    public string ProviderName { get; }

    public string Model { get; }

    public string PayloadJson { get; }
}

/// <summary>
/// The canonical, immutable state of one ordinary LLM conversation: identity, title, provider/model
/// snapshot, timestamps, monotonic transcript revision (the optimistic-concurrency token), ordered
/// transcript entries, the optional in-flight turn marker, and the optional acceleration envelope.
/// Deliberately excludes tools, memory, agent catalog, workspace authority, approvals, finalizers,
/// handoffs, and process semantics.
/// </summary>
public sealed record LlmConversationDocument
{
    public const int MaximumTitleLength = 200;
    public const int MaximumEntries = 10_000;

    public LlmConversationDocument(
        Guid conversationId,
        string title,
        LlmConversationProviderSnapshot provider,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long transcriptRevision,
        ImmutableArray<LlmConversationTranscriptEntry> entries,
        LlmConversationActiveTurn? activeTurn = null,
        LlmConversationAccelerationEnvelope? accelerationState = null)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("A conversation requires a non-empty id.", nameof(conversationId));
        }

        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(provider);
        if (transcriptRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transcriptRevision), transcriptRevision, "A transcript revision cannot be negative.");
        }

        var normalizedTitle = title.Trim();
        if (normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"A conversation title cannot exceed {MaximumTitleLength} characters.", nameof(title));
        }

        if (entries.IsDefault)
        {
            entries = [];
        }

        if (entries.Length > MaximumEntries)
        {
            throw new ArgumentException(
                $"A conversation cannot hold more than {MaximumEntries} transcript entries.", nameof(entries));
        }

        if (entries.Any(entry => entry is null))
        {
            throw new ArgumentException("Transcript entries cannot contain null records.", nameof(entries));
        }

        var entryIds = new HashSet<Guid>();
        if (entries.Any(entry => !entryIds.Add(entry.EntryId)))
        {
            throw new ArgumentException("Transcript entry ids must be unique.", nameof(entries));
        }

        if (activeTurn is not null)
        {
            if (activeTurn.AdmittedRevision != transcriptRevision)
            {
                throw new ArgumentException(
                    "An active turn must record the current transcript revision.", nameof(activeTurn));
            }

            var pendingEntry = entries.Length == 0 ? null : entries[^1];
            if (pendingEntry is null
                || pendingEntry.EntryId != activeTurn.PendingUserEntryId
                || pendingEntry.TurnId != activeTurn.TurnId
                || pendingEntry.Role != LlmMessageRole.User
                || pendingEntry.CreatedAtUtc != activeTurn.AdmittedAtUtc)
            {
                throw new ArgumentException(
                    "An active turn must reference the exact final pending user entry and turn.", nameof(activeTurn));
            }

            if (activeTurn.Compensation?.Provider == provider)
            {
                throw new ArgumentException(
                    "Turn compensation must describe a different pre-adoption provider.", nameof(activeTurn));
            }
        }

        ConversationId = conversationId;
        Title = normalizedTitle;
        Provider = provider;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        TranscriptRevision = transcriptRevision;
        Entries = entries;
        ActiveTurn = activeTurn;
        AccelerationState = accelerationState;
    }

    public Guid ConversationId { get; }

    public string Title { get; init; }

    public LlmConversationProviderSnapshot Provider { get; init; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Monotonic optimistic-concurrency token; every persisted change increments it.</summary>
    public long TranscriptRevision { get; init; }

    public ImmutableArray<LlmConversationTranscriptEntry> Entries { get; init; }

    public LlmConversationActiveTurn? ActiveTurn { get; init; }

    public LlmConversationAccelerationEnvelope? AccelerationState { get; init; }

    /// <summary>Aggregates usage across assistant entries; computed, never stored redundantly.</summary>
    public LlmUsage ComputeTotalUsage()
    {
        var total = LlmUsage.Zero;
        foreach (var entry in Entries)
        {
            if (entry.Usage is { } usage)
            {
                total = total.Add(usage);
            }
        }

        return total;
    }
}

/// <summary>Read-model row for conversation listings.</summary>
public sealed record LlmConversationSummary(
    Guid ConversationId,
    string Title,
    string ProviderName,
    string Model,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long TranscriptRevision,
    int EntryCount,
    bool HasActiveTurn);

/// <summary>
/// How a turn whose provider/model differs from the conversation snapshot is handled.
/// <see cref="Forbid"/> fails the turn typed without touching the transcript; <see cref="Adopt"/>
/// records the new snapshot and invalidates any acceleration state. There is no implicit switching.
/// </summary>
public enum LlmConversationProviderChangePolicy
{
    Forbid,
    Adopt
}

/// <summary>Named failure classes for conversation operations.</summary>
public enum LlmConversationFailureKind
{
    InvalidRequest,
    NotFound,
    AlreadyExists,
    RevisionConflict,
    ConcurrencyConflict,
    TurnAlreadyActive,
    TurnNotActive,
    ProviderModelMismatch,
    StorageCorrupted
}

/// <summary>
/// Typed, sanitized failure for conversation operations. Messages are composed from stable identifiers
/// (failure kind, conversation id, optional stable detail) and never carry transcript text or provider
/// payloads.
/// </summary>
public sealed class LlmConversationException : Exception
{
    public LlmConversationException(
        LlmConversationFailureKind kind,
        Guid conversationId,
        string detail = "",
        Exception? innerException = null)
        : base(BuildMessage(kind, conversationId, detail), innerException)
    {
        Kind = kind;
        ConversationId = conversationId;
    }

    public LlmConversationFailureKind Kind { get; }

    public Guid ConversationId { get; }

    private static string BuildMessage(LlmConversationFailureKind kind, Guid conversationId, string detail)
    {
        var description = kind switch
        {
            LlmConversationFailureKind.InvalidRequest => "The conversation request was invalid.",
            LlmConversationFailureKind.NotFound => "The conversation does not exist.",
            LlmConversationFailureKind.AlreadyExists => "A conversation with this id already exists.",
            LlmConversationFailureKind.RevisionConflict => "The caller's transcript revision is stale.",
            LlmConversationFailureKind.ConcurrencyConflict => "A concurrent update changed the conversation first.",
            LlmConversationFailureKind.TurnAlreadyActive => "Another turn is already in flight for this conversation.",
            LlmConversationFailureKind.TurnNotActive => "The referenced turn is not the active turn.",
            LlmConversationFailureKind.ProviderModelMismatch =>
                "The requested provider/model differs from the conversation snapshot and switching was not requested.",
            LlmConversationFailureKind.StorageCorrupted => "The persisted conversation document could not be read.",
            _ => "The conversation operation failed."
        };
        var detailSuffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail.Trim()}";
        return $"{description} Conversation '{conversationId:N}'.{detailSuffix}";
    }
}

/// <summary>
/// Durable storage boundary for conversation documents. Implementations must apply
/// <see cref="ReplaceAsync"/> atomically: the stored document is replaced only when its current
/// <see cref="LlmConversationDocument.TranscriptRevision"/> equals <c>expectedTranscriptRevision</c> and
/// the incoming document carries a strictly greater revision; otherwise the call fails typed with
/// <see cref="LlmConversationFailureKind.ConcurrencyConflict"/> and the stored document is untouched.
/// This compare-and-swap is what makes concurrent turns unable to corrupt transcript order.
/// </summary>
public interface ILlmConversationStore
{
    /// <summary>Persists a new conversation; fails typed when the id already exists.</summary>
    Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document, CancellationToken cancellationToken = default);

    Task<LlmConversationDocument?> TryGetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Atomic optimistic replacement; see the interface contract for the CAS semantics.</summary>
    Task<LlmConversationDocument> ReplaceAsync(
        LlmConversationDocument document, long expectedTranscriptRevision, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

/// <summary>Input for context-window selection: the canonical entries plus hard outbound bounds.</summary>
public sealed record LlmConversationContextWindowRequest
{
    public LlmConversationContextWindowRequest(
        ImmutableArray<LlmConversationTranscriptEntry> entries,
        int maximumMessages,
        int maximumTotalCharacters)
    {
        if (entries.IsDefault)
        {
            entries = [];
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maximumMessages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTotalCharacters, 1);
        Entries = entries;
        MaximumMessages = maximumMessages;
        MaximumTotalCharacters = maximumTotalCharacters;
    }

    public ImmutableArray<LlmConversationTranscriptEntry> Entries { get; }

    public int MaximumMessages { get; }

    public int MaximumTotalCharacters { get; }
}

/// <summary>
/// Bounded context-window selection seam. A policy shapes only the outbound message window for a single
/// invocation — it never mutates the canonical transcript, so any future summarization policy is
/// non-destructive by construction (it may synthesize summary messages into the window, but destructive
/// transcript compaction is out of a policy's reach and would require its own explicit contract). The
/// returned window must retain the pending user message as its final entry.
/// </summary>
public interface ILlmConversationContextWindowPolicy
{
    IReadOnlyList<LlmMessage> SelectWindow(LlmConversationContextWindowRequest request);
}

/// <summary>Request to create a conversation bound to a provider/model snapshot.</summary>
public sealed record LlmConversationStartRequest
{
    private Guid? conversationId;

    public LlmConversationStartRequest(
        ProviderProfile provider,
        string model = "",
        string title = "",
        string systemPrompt = "")
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Model = model?.Trim() ?? string.Empty;
        Title = title?.Trim() ?? string.Empty;
        SystemPrompt = systemPrompt ?? string.Empty;
        if (Title.Length > LlmConversationDocument.MaximumTitleLength)
        {
            throw new ArgumentException(
                $"A conversation title cannot exceed {LlmConversationDocument.MaximumTitleLength} characters.",
                nameof(title));
        }

        if (SystemPrompt.Length > LlmMessage.MaximumTextLength)
        {
            throw new ArgumentException(
                $"A system prompt cannot exceed {LlmMessage.MaximumTextLength} characters.", nameof(systemPrompt));
        }
    }

    /// <summary>The live provider profile; only its stable identity is persisted.</summary>
    public ProviderProfile Provider { get; }

    public Guid? ConversationId
    {
        get => conversationId;
        init
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A supplied conversation id must be non-empty.", nameof(ConversationId));
            }

            conversationId = value;
        }
    }

    /// <summary>Optional model override; blank selects the provider's default model.</summary>
    public string Model { get; }

    public string Title { get; }

    /// <summary>Optional initial system entry; blank means no system entry is recorded.</summary>
    public string SystemPrompt { get; }
}

/// <summary>
/// One user turn against a conversation. Carries the caller's view of the transcript revision for
/// optimistic concurrency, the live provider profile for the invocation, and pass-through
/// response-format/model-settings/deadline/correlation preferences for the stateless port. Payload text
/// is data — it never selects authority, workspace, tools, or processes.
/// </summary>
public sealed record LlmConversationTurnRequest
{
    private Guid? turnId;

    public LlmConversationTurnRequest(
        Guid conversationId,
        long expectedTranscriptRevision,
        string userText,
        ProviderProfile provider,
        string model = "",
        LlmConversationProviderChangePolicy providerChangePolicy = LlmConversationProviderChangePolicy.Forbid,
        LlmResponseFormat? responseFormat = null,
        LlmModelSettings? settings = null,
        TimeSpan? timeout = null,
        string correlationId = "")
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("A turn requires a non-empty conversation id.", nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(expectedTranscriptRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        if (userText.Length > LlmMessage.MaximumTextLength)
        {
            throw new ArgumentException(
                $"A user message cannot exceed {LlmMessage.MaximumTextLength} characters.", nameof(userText));
        }

        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        if (!Enum.IsDefined(providerChangePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerChangePolicy), providerChangePolicy, "Unknown provider change policy.");
        }

        var normalizedCorrelationId = correlationId?.Trim() ?? string.Empty;
        if (normalizedCorrelationId.Length > LlmInvocationRequest.MaximumCorrelationIdLength)
        {
            throw new ArgumentException(
                $"A correlation id cannot exceed {LlmInvocationRequest.MaximumCorrelationIdLength} characters.",
                nameof(correlationId));
        }

        ConversationId = conversationId;
        ExpectedTranscriptRevision = expectedTranscriptRevision;
        UserText = userText;
        Model = model?.Trim() ?? string.Empty;
        ProviderChangePolicy = providerChangePolicy;
        ResponseFormat = responseFormat;
        Settings = settings;
        Timeout = timeout;
        CorrelationId = normalizedCorrelationId;
    }

    public Guid ConversationId { get; }

    public Guid? TurnId
    {
        get => turnId;
        init
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A supplied turn id must be non-empty.", nameof(TurnId));
            }

            turnId = value;
        }
    }

    public long ExpectedTranscriptRevision { get; }

    public string UserText { get; }

    public ProviderProfile Provider { get; }

    /// <summary>Optional model override; blank keeps the conversation's snapshot model.</summary>
    public string Model { get; }

    public LlmConversationProviderChangePolicy ProviderChangePolicy { get; }

    public LlmResponseFormat? ResponseFormat { get; }

    public LlmModelSettings? Settings { get; }

    public TimeSpan? Timeout { get; }

    public string CorrelationId { get; }
}

/// <summary>The completed turn: the updated conversation plus the two entries the turn appended.</summary>
public sealed record LlmConversationTurnResult(
    LlmConversationDocument Conversation,
    LlmConversationTranscriptEntry UserEntry,
    LlmConversationTranscriptEntry AssistantEntry);

public sealed record LlmConversationAdmittedTurnRequest
{
    public LlmConversationAdmittedTurnRequest(
        Guid conversationId,
        Guid turnId,
        ProviderProfile provider,
        string model = "",
        LlmResponseFormat? responseFormat = null,
        LlmModelSettings? settings = null,
        TimeSpan? timeout = null,
        string correlationId = "")
    {
        ArgumentOutOfRangeException.ThrowIfEqual(conversationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, Guid.Empty);
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        var normalizedCorrelationId = correlationId?.Trim() ?? string.Empty;
        if (normalizedCorrelationId.Length > LlmInvocationRequest.MaximumCorrelationIdLength)
        {
            throw new ArgumentException(
                $"A correlation id cannot exceed {LlmInvocationRequest.MaximumCorrelationIdLength} characters.",
                nameof(correlationId));
        }

        ConversationId = conversationId;
        TurnId = turnId;
        Model = model?.Trim() ?? string.Empty;
        ResponseFormat = responseFormat;
        Settings = settings;
        Timeout = timeout;
        CorrelationId = normalizedCorrelationId;
    }

    public Guid ConversationId { get; }

    public Guid TurnId { get; }

    public ProviderProfile Provider { get; }

    public string Model { get; }

    public LlmResponseFormat? ResponseFormat { get; }

    public LlmModelSettings? Settings { get; }

    public TimeSpan? Timeout { get; }

    public string CorrelationId { get; }
}

public sealed record LlmConversationTurnAdmission(
    LlmConversationDocument Conversation,
    LlmConversationTranscriptEntry UserEntry,
    LlmInvocationRequest InvocationRequest);

/// <summary>
/// Application service for ordinary multi-turn LLM conversations, layered strictly above
/// <see cref="ILlmInvocationPort"/>. It owns transcript persistence, conversation metadata, atomic
/// turn admission, provider/model switch policy, and context-window selection, delegating every
/// inference call to the stateless port. Implementations must not construct agents, sessions, tools,
/// memory, context contributors, or workspace authority, and must not treat provider conversation
/// state as canonical.
/// </summary>
public interface ILlmConversationService
{
    Task<LlmConversationDocument> StartAsync(
        LlmConversationStartRequest request, CancellationToken cancellationToken = default);

    Task<LlmConversationDocument?> TryGetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<LlmConversationDocument> RenameAsync(
        Guid conversationId, string title, long expectedTranscriptRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Admits and completes one turn atomically: the pending user entry and in-flight marker are
    /// persisted via revision CAS before invocation, and the turn either completes with an assistant
    /// entry or rolls the transcript back to its pre-turn content. Concurrent admission attempts fail
    /// typed instead of corrupting transcript order.
    /// </summary>
    Task<LlmConversationTurnResult> SendAsync(
        LlmConversationTurnRequest request, CancellationToken cancellationToken = default);

    Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmConversationTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmConversationAdmittedTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmConversationTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default);

    Task<LlmConversationDocument> CompensateTurnAsync(
        Guid conversationId,
        Guid turnId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicit recovery for a turn orphaned by a crash: removes the pending user entry and clears the
    /// in-flight marker. Requires the exact active turn id; never applied heuristically.
    /// </summary>
    Task<LlmConversationDocument> AbandonActiveTurnAsync(
        Guid conversationId, Guid turnId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

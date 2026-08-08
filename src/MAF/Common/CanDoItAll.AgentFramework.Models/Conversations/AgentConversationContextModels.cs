using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifies one context epoch of a conversation. A new epoch starts when the
/// followed source entity or source kind changes, or when the conversation is
/// detached or its context becomes unavailable. Prior-epoch UI facts are
/// historical context only.
/// </summary>
public readonly record struct AgentContextEpochId
{
    [JsonConstructor]
    public AgentContextEpochId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A context epoch id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentContextEpochId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

/// <summary>
/// What a floating conversation follows. <c>PinnedToSource</c> is deliberately
/// deferred: pinning an inactive source requires a canonical rehydrator that
/// can rebuild fresh facts without reusing stale opaque UI attachments, and no
/// such rehydrator exists yet.
/// </summary>
public enum AgentConversationContextMode
{
    FollowCurrentSurface = 0,
    Detached = 1
}

/// <summary>
/// Monotonic revision of one conversation context binding. Updates use
/// expected-revision compare-and-swap semantics.
/// </summary>
public readonly record struct AgentConversationBindingRevision
{
    [JsonConstructor]
    public AgentConversationBindingRevision(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A conversation binding revision must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public AgentConversationBindingRevision Next()
        => new(checked(Value + 1));

    public override string ToString()
        => Value.ToString();
}

/// <summary>
/// Durable record of what one floating chat thread is following. The binding
/// is keyed by the floating chat handle before a chat session exists and by
/// the chat session once one is created; at least one key is always present.
/// The binding never grants execution authority.
/// </summary>
public sealed record AgentConversationContextBinding
{
    public AgentConversationContextBinding(
        AgentConversationContextMode mode,
        AgentContextEpochId contextEpochId,
        AgentConversationBindingRevision revision,
        DateTimeOffset adoptedAtUtc,
        DateTimeOffset updatedAtUtc,
        AgentChatHandleId? handleId = null,
        Guid? chatSessionId = null,
        AgentChatContextSourceKind? sourceKind = null,
        AgentChatContextSourceId? sourceId = null,
        string displayName = "",
        string lastSurface = "",
        string lastView = "",
        string lastTurnContextDigest = "",
        string lastSelectionId = "")
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown conversation context mode.");
        }

        if (contextEpochId.IsEmpty)
        {
            throw new ArgumentException("A context epoch id is required.", nameof(contextEpochId));
        }

        if (handleId is { IsEmpty: true })
        {
            throw new ArgumentException("A floating chat handle id cannot be empty when present.", nameof(handleId));
        }

        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException("A chat session id cannot be empty when present.", nameof(chatSessionId));
        }

        if (handleId is null && chatSessionId is null)
        {
            throw new ArgumentException(
                "A conversation context binding requires a floating chat handle or a chat session identity.",
                nameof(handleId));
        }

        if (mode == AgentConversationContextMode.FollowCurrentSurface)
        {
            if (sourceKind is { IsEmpty: true } || sourceId is { IsEmpty: true })
            {
                throw new ArgumentException(
                    "A followed source identity cannot be empty when present.",
                    nameof(sourceKind));
            }
        }

        if (mode == AgentConversationContextMode.Detached && (sourceKind is not null || sourceId is not null))
        {
            throw new ArgumentException(
                "A detached conversation cannot claim a followed source identity.",
                nameof(sourceKind));
        }

        var normalizedDisplayName = displayName?.Trim() ?? string.Empty;
        if (normalizedDisplayName.Length > AgentChatContextLimits.MaximumDisplayNameLength)
        {
            throw new ArgumentException(
                $"A conversation display name cannot exceed {AgentChatContextLimits.MaximumDisplayNameLength} characters.",
                nameof(displayName));
        }

        var normalizedDigest = lastTurnContextDigest?.Trim() ?? string.Empty;
        if (normalizedDigest.Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A turn context digest cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                nameof(lastTurnContextDigest));
        }

        Mode = mode;
        ContextEpochId = contextEpochId;
        Revision = revision;
        AdoptedAtUtc = adoptedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        HandleId = handleId;
        ChatSessionId = chatSessionId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        DisplayName = normalizedDisplayName;
        LastSurface = lastSurface?.Trim() ?? string.Empty;
        LastView = lastView?.Trim() ?? string.Empty;
        LastTurnContextDigest = normalizedDigest;
        LastSelectionId = lastSelectionId?.Trim() ?? string.Empty;
    }

    public AgentConversationContextMode Mode { get; }

    public AgentContextEpochId ContextEpochId { get; }

    public AgentConversationBindingRevision Revision { get; }

    public DateTimeOffset AdoptedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public AgentChatHandleId? HandleId { get; }

    public Guid? ChatSessionId { get; }

    public AgentChatContextSourceKind? SourceKind { get; }

    public AgentChatContextSourceId? SourceId { get; }

    public string DisplayName { get; }

    public string LastSurface { get; }

    public string LastView { get; }

    public string LastTurnContextDigest { get; }

    public string LastSelectionId { get; }

    public bool IsFollowing => Mode == AgentConversationContextMode.FollowCurrentSurface
        && SourceKind is { IsEmpty: false }
        && SourceId is { IsEmpty: false };

    /// <summary>
    /// Creates the initial binding for a newly opened floating chat that
    /// follows the current surface but has not adopted a source yet.
    /// </summary>
    public static AgentConversationContextBinding CreatePendingFollow(
        AgentChatHandleId handleId,
        DateTimeOffset nowUtc)
        => new(
            AgentConversationContextMode.FollowCurrentSurface,
            AgentContextEpochId.Create(),
            new AgentConversationBindingRevision(1),
            nowUtc,
            nowUtc,
            handleId);
}

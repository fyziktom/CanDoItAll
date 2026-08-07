namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Relationship between a conversation's previous accepted context binding and
/// the newly captured observation. A transition is application-generated
/// trusted metadata computed from typed identities; it is never derived from
/// user or model prose and is supplied on the next explicit turn only.
/// </summary>
public enum AgentContextTransitionKind
{
    None = 0,
    ViewChanged = 1,
    SelectionChanged = 2,
    SourceEntityChanged = 3,
    SourceKindChanged = 4,
    ContextDetached = 5,
    ContextUnavailable = 6
}

/// <summary>
/// What the conversation decided to do with the observed transition.
/// </summary>
public enum AgentContextTransitionDecision
{
    Kept = 0,
    AutoAdopted = 1,
    Detached = 2,
    Rejected = 3
}

/// <summary>
/// Whether the transition keeps the current context epoch or starts a new one.
/// Same-source view/selection changes keep the epoch; source-entity,
/// source-kind, detach, and unavailable transitions start a new epoch.
/// </summary>
public enum AgentContextEpochBehavior
{
    KeepEpoch = 0,
    NewEpoch = 1
}

/// <summary>
/// Classification data for one context transition. SB01 defines the data
/// shape only; the deterministic classifier that produces it is owned by the
/// conversation context service (SB03). The bounded summary is safe
/// model-facing text and must not carry authorization details.
/// </summary>
public sealed record AgentContextTransition
{
    public const int MaximumSummaryLength = 1_000;

    public AgentContextTransition(
        AgentContextTransitionKind kind,
        AgentContextTransitionDecision decision,
        AgentContextEpochBehavior epochBehavior,
        AgentChatContextSourceKind? previousSourceKind = null,
        AgentChatContextSourceId? previousSourceId = null,
        string? previousView = null,
        AgentChatContextSourceKind? currentSourceKind = null,
        AgentChatContextSourceId? currentSourceId = null,
        string? currentView = null,
        long? previousBindingRevision = null,
        string summary = "")
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown context transition kind.");
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown context transition decision.");
        }

        if (!Enum.IsDefined(epochBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(epochBehavior), epochBehavior, "Unknown context epoch behavior.");
        }

        if (previousBindingRevision is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousBindingRevision),
                previousBindingRevision,
                "A previous binding revision must be positive when present.");
        }

        var normalizedSummary = summary?.Trim() ?? string.Empty;
        if (normalizedSummary.Length > MaximumSummaryLength)
        {
            throw new ArgumentException(
                $"A transition summary cannot exceed {MaximumSummaryLength} characters.",
                nameof(summary));
        }

        Kind = kind;
        Decision = decision;
        EpochBehavior = epochBehavior;
        PreviousSourceKind = previousSourceKind;
        PreviousSourceId = previousSourceId;
        PreviousView = previousView?.Trim();
        CurrentSourceKind = currentSourceKind;
        CurrentSourceId = currentSourceId;
        CurrentView = currentView?.Trim();
        PreviousBindingRevision = previousBindingRevision;
        Summary = normalizedSummary;
    }

    public AgentContextTransitionKind Kind { get; }

    public AgentContextTransitionDecision Decision { get; }

    public AgentContextEpochBehavior EpochBehavior { get; }

    public AgentChatContextSourceKind? PreviousSourceKind { get; }

    public AgentChatContextSourceId? PreviousSourceId { get; }

    public string? PreviousView { get; }

    public AgentChatContextSourceKind? CurrentSourceKind { get; }

    public AgentChatContextSourceId? CurrentSourceId { get; }

    public string? CurrentView { get; }

    public long? PreviousBindingRevision { get; }

    /// <summary>Bounded safe model-facing description, e.g. "Canvas -> Gantt".</summary>
    public string Summary { get; }

    public static AgentContextTransition None { get; } = new(
        AgentContextTransitionKind.None,
        AgentContextTransitionDecision.Kept,
        AgentContextEpochBehavior.KeepEpoch);
}

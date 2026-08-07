using System.Collections.Immutable;

using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifies one captured live UI observation. An observation identifier is
/// runtime-generated per capture and is never reused across captures.
/// </summary>
public readonly record struct AgentUiObservationId
{
    [JsonConstructor]
    public AgentUiObservationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A UI observation id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentUiObservationId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

/// <summary>
/// Completeness of a captured observation. A turn that admits a partial or
/// loading observation must state so explicitly instead of silently reusing
/// stale facts from an earlier capture.
/// </summary>
public enum AgentUiObservationCompleteness
{
    Ready = 0,
    Loading = 1,
    Partial = 2,
    Failed = 3,
    Unavailable = 4
}

/// <summary>
/// Reference to an opaque module-owned attachment that participated in an
/// observation. The durable reference carries identity and fingerprints only;
/// the opaque payload itself stays request-scoped in the runtime lease and is
/// never serialized with observation or turn records.
/// </summary>
public sealed record AgentUiObservationAttachmentReference
{
    public AgentUiObservationAttachmentReference(
        AgentChatContextAttachmentKind kind,
        AgentChatContextContributorId contributorId,
        SnapshotContentFingerprint contentFingerprint,
        SnapshotCoverageFingerprint coverageFingerprint,
        SnapshotFreshnessFingerprint freshnessFingerprint,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? freshUntilUtc)
    {
        if (kind.IsEmpty)
        {
            throw new ArgumentException("An attachment kind is required.", nameof(kind));
        }

        if (contributorId.IsEmpty)
        {
            throw new ArgumentException("An attachment contributor id is required.", nameof(contributorId));
        }

        Kind = kind;
        ContributorId = contributorId;
        ContentFingerprint = contentFingerprint;
        CoverageFingerprint = coverageFingerprint;
        FreshnessFingerprint = freshnessFingerprint;
        CapturedAtUtc = capturedAtUtc;
        FreshUntilUtc = freshUntilUtc;
    }

    public AgentChatContextAttachmentKind Kind { get; }

    public AgentChatContextContributorId ContributorId { get; }

    public SnapshotContentFingerprint ContentFingerprint { get; }

    public SnapshotCoverageFingerprint CoverageFingerprint { get; }

    public SnapshotFreshnessFingerprint FreshnessFingerprint { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public DateTimeOffset? FreshUntilUtc { get; }
}

/// <summary>
/// One bounded fact contributed to a live UI observation. Facts are untrusted
/// model context; they never grant product access or execution authority.
/// </summary>
public sealed record AgentUiObservationFact
{
    public AgentUiObservationFact(
        AgentChatContextContributorId contributorId,
        int order,
        string content)
    {
        if (contributorId.IsEmpty)
        {
            throw new ArgumentException("An observation fact contributor id is required.", nameof(contributorId));
        }

        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "An observation fact order cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(content);
        var normalized = content.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("An observation fact requires content.", nameof(content));
        }

        if (normalized.Length > AgentChatContextFragment.MaximumContentLength)
        {
            throw new ArgumentException(
                $"An observation fact cannot exceed {AgentChatContextFragment.MaximumContentLength} characters.",
                nameof(content));
        }

        ContributorId = contributorId;
        Order = order;
        Content = normalized;
    }

    public AgentChatContextContributorId ContributorId { get; }

    public int Order { get; }

    public string Content { get; }
}

/// <summary>
/// Canonical description of what the user is currently looking at, captured as
/// one immutable snapshot at turn admission. It is ephemeral, versioned,
/// route-fenced, untrusted as model content, and is not an authorization
/// object: an expected source identity inside an observation is only a request
/// to the canonical authority resolver.
/// </summary>
public sealed record AgentUiObservationSnapshot
{
    public AgentUiObservationSnapshot(
        AgentUiObservationId observationId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        string displayName,
        string surface,
        string view,
        long publicationVersion,
        AgentUiObservationCompleteness completeness,
        DateTimeOffset capturedAtUtc,
        WorkspaceScopeDescriptor? expectedWorkspaceScope = null,
        AgentChatContextScopeId? scopeId = null,
        AgentChatNavigationIdentity? navigationIdentity = null,
        AgentChatContextEntityReference? primarySelection = null,
        IReadOnlyList<AgentChatContextEntityReference>? selectedEntities = null,
        IReadOnlyList<AgentUiObservationFact>? visibleFacts = null,
        IReadOnlyList<AgentUiObservationAttachmentReference>? attachmentReferences = null,
        DateTimeOffset? freshUntilUtc = null)
    {
        if (observationId.IsEmpty)
        {
            throw new ArgumentException("An observation id is required.", nameof(observationId));
        }

        if (sourceKind.IsEmpty)
        {
            throw new ArgumentException("An observation source kind is required.", nameof(sourceKind));
        }

        if (sourceId.IsEmpty)
        {
            throw new ArgumentException("An observation source id is required.", nameof(sourceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Trim().Length > AgentChatContextLimits.MaximumDisplayNameLength)
        {
            throw new ArgumentException(
                $"An observation display name cannot exceed {AgentChatContextLimits.MaximumDisplayNameLength} characters.",
                nameof(displayName));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        if (surface.Trim().Length > AgentChatContextLimits.MaximumSourceKindLength)
        {
            throw new ArgumentException(
                $"An observation surface cannot exceed {AgentChatContextLimits.MaximumSourceKindLength} characters.",
                nameof(surface));
        }

        if (view.Trim().Length > AgentChatContextLimits.MaximumSourceKindLength)
        {
            throw new ArgumentException(
                $"An observation view cannot exceed {AgentChatContextLimits.MaximumSourceKindLength} characters.",
                nameof(view));
        }

        if (publicationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publicationVersion),
                publicationVersion,
                "An observation publication version must be positive.");
        }

        var facts = visibleFacts?.ToImmutableArray() ?? [];
        if (facts.Length > AgentChatContextLimits.MaximumFragments)
        {
            throw new ArgumentException(
                $"An observation cannot carry more than {AgentChatContextLimits.MaximumFragments} facts.",
                nameof(visibleFacts));
        }

        ObservationId = observationId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        DisplayName = displayName.Trim();
        Surface = surface.Trim();
        View = view.Trim();
        PublicationVersion = publicationVersion;
        Completeness = completeness;
        CapturedAtUtc = capturedAtUtc;
        ExpectedWorkspaceScope = expectedWorkspaceScope;
        ScopeId = scopeId;
        NavigationIdentity = navigationIdentity;
        PrimarySelection = primarySelection;
        SelectedEntities = selectedEntities?.ToImmutableArray() ?? [];
        VisibleFacts = facts;
        AttachmentReferences = attachmentReferences?.ToImmutableArray() ?? [];
        FreshUntilUtc = freshUntilUtc;
    }

    public AgentUiObservationId ObservationId { get; }

    public AgentChatContextSourceKind SourceKind { get; }

    public AgentChatContextSourceId SourceId { get; }

    public string DisplayName { get; }

    public string Surface { get; }

    public string View { get; }

    public long PublicationVersion { get; }

    public AgentUiObservationCompleteness Completeness { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    /// <summary>
    /// The workspace scope the publishing surface expects. This is a request to
    /// the authority resolver, never a grant.
    /// </summary>
    public WorkspaceScopeDescriptor? ExpectedWorkspaceScope { get; }

    public AgentChatContextScopeId? ScopeId { get; }

    public AgentChatNavigationIdentity? NavigationIdentity { get; }

    public AgentChatContextEntityReference? PrimarySelection { get; }

    public IReadOnlyList<AgentChatContextEntityReference> SelectedEntities { get; }

    public IReadOnlyList<AgentUiObservationFact> VisibleFacts { get; }

    public IReadOnlyList<AgentUiObservationAttachmentReference> AttachmentReferences { get; }

    public DateTimeOffset? FreshUntilUtc { get; }
}

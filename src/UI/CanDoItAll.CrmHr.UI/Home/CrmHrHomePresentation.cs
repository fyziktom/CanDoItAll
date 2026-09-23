namespace CanDoItAll.CrmHr.UI.Home;

// Read phases of the Home overview. Only a Ready presentation carries an accepted overview; Loading and Failed
// render placeholders and the failure copy so that no section claims a zero result before a read succeeded.
public enum CrmHrHomePhase
{
    Loading,
    Ready,
    Failed
}

// Where a Home navigation action leads. The host maps a destination to its route; the renderer never builds URLs.
public enum CrmHrHomeDestination
{
    Directory,
    Crm,
    Workforce,
    Recruiting,
    Agents,
    Assignments
}

// Totals reported by the application query owner. They are never derived from the truncated preview lists.
public sealed record CrmHrHomeTotals(
    int Parties,
    int Organizations,
    int Opportunities,
    int WorkforceProfiles,
    int AgentProjections,
    int SensitiveRecords);

// One directory preview row: safe display values plus the party identity for future actions.
public sealed record CrmHrHomeDirectoryEntry(
    Guid PartyId,
    string DisplayName,
    string TypeLabel,
    string LifecycleLabel,
    string? Summary,
    bool IsSensitive);

// One sensitive preview row. Deliberately narrower than the directory row: no operational summary is shown here.
public sealed record CrmHrHomeSensitiveEntry(
    Guid PartyId,
    string DisplayName,
    string TypeLabel,
    string LifecycleLabel);

// One open-pipeline preview row. The account and opportunity identities stay structured for the detail link.
public sealed record CrmHrHomeOpportunityEntry(
    Guid OpportunityId,
    Guid AccountPartyId,
    string Title,
    string AccountDisplayName,
    string OwnerDisplayName,
    string StageLabel,
    string SourceLabel,
    decimal? Amount,
    int ProbabilityPercent);

// The accepted overview: totals and the three previews of one successful read. Lists are copied on construction so
// a later change to a supplied collection cannot alter an accepted overview behind the renderer.
public sealed record CrmHrHomeOverview
{
    public CrmHrHomeOverview(
        CrmHrHomeTotals totals,
        IReadOnlyList<CrmHrHomeDirectoryEntry> directoryPreview,
        IReadOnlyList<CrmHrHomeSensitiveEntry> sensitivePreview,
        IReadOnlyList<CrmHrHomeOpportunityEntry> openPipelinePreview)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentNullException.ThrowIfNull(directoryPreview);
        ArgumentNullException.ThrowIfNull(sensitivePreview);
        ArgumentNullException.ThrowIfNull(openPipelinePreview);
        Totals = totals;
        DirectoryPreview = directoryPreview.ToArray();
        SensitivePreview = sensitivePreview.ToArray();
        OpenPipelinePreview = openPipelinePreview.ToArray();
    }

    public CrmHrHomeTotals Totals { get; }

    public IReadOnlyList<CrmHrHomeDirectoryEntry> DirectoryPreview { get; }

    public IReadOnlyList<CrmHrHomeSensitiveEntry> SensitivePreview { get; }

    public IReadOnlyList<CrmHrHomeOpportunityEntry> OpenPipelinePreview { get; }

    public static CrmHrHomeOverview Empty { get; } = new(new CrmHrHomeTotals(0, 0, 0, 0, 0, 0), [], [], []);
}

// Everything the Home surface renders. Generation identifies the host attempt an intent refers to.
public sealed record CrmHrHomePresentation(
    long Generation,
    CrmHrHomePhase Phase,
    CrmHrHomeOverview? Overview,
    string? FailureMessage,
    bool IsRetrying)
{
    public bool HasOverview => Phase == CrmHrHomePhase.Ready && Overview is not null;

    public static CrmHrHomePresentation CreateLoading(long generation)
        => new(generation, CrmHrHomePhase.Loading, null, null, IsRetrying: false);

    public static CrmHrHomePresentation CreateReady(long generation, CrmHrHomeOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        return new CrmHrHomePresentation(generation, CrmHrHomePhase.Ready, overview, null, IsRetrying: false);
    }

    public static CrmHrHomePresentation CreateFailed(long generation, string failureMessage, bool isRetrying = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
        return new CrmHrHomePresentation(generation, CrmHrHomePhase.Failed, null, failureMessage, isRetrying);
    }
}

// Typed intents emitted by the Home surface. The host owns navigation and the read lifecycle.
public abstract record CrmHrHomeIntent
{
    private CrmHrHomeIntent()
    {
    }

    public sealed record Navigate(CrmHrHomeDestination Destination) : CrmHrHomeIntent;

    public sealed record OpenOpportunity(Guid AccountPartyId, Guid OpportunityId) : CrmHrHomeIntent;

    public sealed record Retry(long Generation) : CrmHrHomeIntent;
}

public static class CrmHrHomeText
{
    public const string LoadingValue = "…";
    public const string UnavailableValue = "—";

    // Header stat values: real totals only for an accepted overview, otherwise an explicit placeholder.
    public static string FormatStat(CrmHrHomePresentation presentation, Func<CrmHrHomeTotals, int> selector)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(selector);
        return presentation.HasOverview
            ? selector(presentation.Overview!.Totals).ToString()
            : presentation.Phase == CrmHrHomePhase.Loading ? LoadingValue : UnavailableValue;
    }

    public static string FormatAmount(decimal amount) => amount.ToString("0.##");

    public static string FormatProbability(int probabilityPercent) => $"{probabilityPercent}%";
}

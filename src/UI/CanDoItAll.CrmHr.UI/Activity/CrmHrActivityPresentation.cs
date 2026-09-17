namespace CanDoItAll.CrmHr.UI.Activity;

// Status tones an activity entry renders; the host maps its own kinds and types to them.
public enum CrmHrActivityTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

// One timeline row as the surface shows it: kind, text, metadata line, timestamp, tone and overdue flag already
// resolved by the host.
public sealed record CrmHrActivityEntry(
    Guid Id,
    string Kind,
    string Title,
    string? Description,
    string Meta,
    DateTimeOffset OccurredAtUtc,
    CrmHrActivityTone Tone,
    bool IsOverdue);

// One page of activity with the totals of the whole history; the counts cover every entry, the items only this page.
public sealed record CrmHrActivityPage(
    IReadOnlyList<CrmHrActivityEntry> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int ActionCount,
    int OverdueActionCount)
{
    public const int DefaultPageSize = 10;

    public IReadOnlyList<CrmHrActivityEntry> Items { get; init; } = Items?.ToArray() ?? throw new ArgumentNullException(nameof(Items));

    public int TotalPages => TotalCount <= 0 || PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static CrmHrActivityPage Empty(int pageSize = DefaultPageSize)
        => new([], 0, pageSize, 0, 0, 0);
}

// What the host has accepted for its current target and whether a read is in flight. No accepted page means the
// history is unknown for the target: totals and pages are unavailable, never zero. An accepted page with a read in
// flight is another page of the same accepted history loading over its totals.
public sealed record CrmHrActivityPresentation(CrmHrActivityPage? Accepted, bool IsLoading)
{
    public static CrmHrActivityPresentation NotAccepted { get; } = new(null, false);

    public static CrmHrActivityPresentation Loading(CrmHrActivityPage? accepted = null) => new(accepted, true);

    public static CrmHrActivityPresentation Ready(CrmHrActivityPage accepted)
        => new(accepted ?? throw new ArgumentNullException(nameof(accepted)), false);

    public bool HasAccepted => Accepted is not null;
}

// The host-specific wording of a timeline: heading and empty-state copy. Each host composition supplies its own.
public sealed record CrmHrActivityCopy(
    string Eyebrow,
    string Title,
    string Description,
    string EmptyEyebrow,
    string EmptyTitle,
    string EmptyDescription)
{
    public static CrmHrActivityCopy Default { get; } = new(
        "Timeline",
        "Recent interactions and changes",
        "Use this view before customer meetings or handoffs so the latest conversations, follow-ups, and CRM edits are visible in one place.",
        "No activity yet",
        "Log the first CRM interaction",
        "Timeline entries appear here after you save CRM interactions or update account details.");
}

// Everything the timeline can ask its host to do: only another page of the same history.
public abstract record CrmHrActivityIntent
{
    private CrmHrActivityIntent()
    {
    }

    public sealed record RequestPage(int PageIndex) : CrmHrActivityIntent;
}

public static class CrmHrActivityText
{
    public const string NoPagesValue = "No pages";
    public const string OverdueLabel = "Overdue";
    public const string TotalsLoadingLabel = "Counts load with the history";
    public const string TotalsUnavailableLabel = "Counts unavailable";
    public const string HistoryUnavailableTitle = "History not loaded";
    public const string HistoryUnavailableDescription = "No activity has been accepted for this record yet.";

    public static string FormatActivities(int count) => $"{count:N0} activities";

    public static string FormatNextActions(int count) => $"{count:N0} next actions";

    public static string FormatOverdue(int count) => $"{count:N0} overdue";

    public static string FormatPage(CrmHrActivityPage page)
        => page.TotalPages == 0
            ? NoPagesValue
            : $"Page {page.PageIndex + 1:N0} of {page.TotalPages:N0}";

    // The local short date and time, as the module rendered it before the extraction.
    public static string FormatTimestamp(DateTimeOffset occurredAtUtc)
        => occurredAtUtc.LocalDateTime.ToString("g");

    public static string ToneToken(CrmHrActivityTone tone)
        => tone switch
        {
            CrmHrActivityTone.Info => "info",
            CrmHrActivityTone.Success => "success",
            CrmHrActivityTone.Warning => "warning",
            CrmHrActivityTone.Danger => "danger",
            _ => "neutral"
        };
}

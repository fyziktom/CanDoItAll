using CanDoItAll.CrmHr.UI.Activity;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

// Owns one host's activity history lane: the target party, the page accepted for it, the busy gate for further page
// requests, retry admission and the fencing of late outcomes. Which query answers (account activity or party
// activity), how the host renders a failure and when the host asks for the history stay with the host; the session
// never renders or navigates. It is used on the renderer's dispatcher only.
public sealed class CrmHrActivityHistorySession : IDisposable
{
    public delegate Task<CrmActivityHistoryPage> ActivityQuery(CrmActivityHistoryQuery query, CancellationToken cancellationToken);

    private readonly ActivityQuery query;
    private readonly string initialFailureMessage;
    private readonly string pageFailureMessage;
    private readonly ILogger? logger;
    private readonly int pageSize;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? read;
    private Task? activeRead;
    private long generation;
    private bool disposed;

    public CrmHrActivityHistorySession(
        ActivityQuery query,
        string initialFailureMessage,
        string pageFailureMessage,
        ILogger? logger = null,
        int pageSize = CrmActivityHistoryQueryLimits.DefaultPageSize)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.initialFailureMessage = initialFailureMessage ?? throw new ArgumentNullException(nameof(initialFailureMessage));
        this.pageFailureMessage = pageFailureMessage ?? throw new ArgumentNullException(nameof(pageFailureMessage));
        this.logger = logger;
        this.pageSize = pageSize;
    }

    // The party whose history this lane currently serves; null after invalidation.
    public Guid? Target { get; private set; }

    // What the host may show: the page accepted for the target and whether a read is in flight.
    public CrmHrActivityPresentation Presentation { get; private set; } = CrmHrActivityPresentation.NotAccepted;

    // Safe copy of the most recent failed read for the target; null while nothing failed or after a new admission.
    public string? FailureMessage { get; private set; }

    // Makes sure the first page of the target is accepted or being read. A same-target call joins the read in
    // flight, returns at once when a page is accepted, and does not replay a failed read (retry is explicit).
    // Another target retires the current lane, so a late outcome of the previous target can never be shown as this one.
    public Task EnsureLoadedAsync(Guid partyId)
    {
        if (disposed || partyId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        if (Target == partyId)
        {
            if (activeRead is { } pending)
            {
                return pending;
            }

            if (Presentation.HasAccepted || FailureMessage is not null)
            {
                return Task.CompletedTask;
            }
        }

        return BeginRead(partyId, pageIndex: 0, accepted: null);
    }

    // Another page of the accepted history. Rejected while a read is in flight, before a page was accepted and
    // outside the accepted range; the accepted totals stay visible while the page loads.
    public Task RequestPageAsync(int pageIndex)
    {
        if (disposed ||
            Target is not { } target ||
            Presentation is not { IsLoading: false, Accepted: { } accepted } ||
            pageIndex < 0 ||
            pageIndex >= accepted.TotalPages)
        {
            return Task.CompletedTask;
        }

        return BeginRead(target, pageIndex, accepted);
    }

    // Reads the target's first page again after a failure. Duplicate admissions while a read is in flight and
    // retries without a failure are ignored; a retry never turns into an implicit refresh of an accepted history.
    public Task RetryAsync()
    {
        if (disposed || Target is not { } target || FailureMessage is null || Presentation.IsLoading)
        {
            return Task.CompletedTask;
        }

        return BeginRead(target, pageIndex: 0, accepted: null);
    }

    // Forgets the target and everything accepted for it and retires the read in flight.
    public void Invalidate()
    {
        Retire();
        Target = null;
        FailureMessage = null;
        Presentation = CrmHrActivityPresentation.NotAccepted;
    }

    private Task BeginRead(Guid partyId, int pageIndex, CrmHrActivityPage? accepted)
    {
        Retire();
        var current = generation;
        var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        read = source;
        Target = partyId;
        FailureMessage = null;
        Presentation = CrmHrActivityPresentation.Loading(accepted);
        return activeRead = ReadAsync(partyId, pageIndex, accepted, current, source);
    }

    private async Task ReadAsync(Guid partyId, int pageIndex, CrmHrActivityPage? accepted, long current, CancellationTokenSource source)
    {
        try
        {
            var page = await query(
                new CrmActivityHistoryQuery(partyId, pageIndex, accepted?.PageSize ?? pageSize),
                source.Token);
            if (!IsCurrent(partyId, current))
            {
                return;
            }

            Presentation = CrmHrActivityPresentation.Ready(CrmHrActivityPresentationMapper.ToPage(page));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsCurrent(partyId, current))
            {
                return;
            }

            // The host gets safe copy; the exception goes to the log only. A failed page read keeps the accepted page.
            logger?.LogError(exception, "The activity history read for party {PartyId} (page {PageIndex}) failed.", partyId, pageIndex);
            FailureMessage = accepted is null ? initialFailureMessage : pageFailureMessage;
            Presentation = new CrmHrActivityPresentation(accepted, IsLoading: false);
        }
        finally
        {
            if (IsCurrent(partyId, current))
            {
                activeRead = null;
                read = null;
            }

            // The query has returned, so nothing can still need this token source.
            source.Dispose();
        }
    }

    // Cancels the read in flight and advances the generation, so its late success, failure or completion is inert.
    // The retired read keeps its own token source until it ends and never clears a newer lane.
    private void Retire()
    {
        generation++;
        var retired = read;
        read = null;
        activeRead = null;
        retired?.Cancel();
    }

    private bool IsCurrent(Guid partyId, long current)
        => !disposed && Target == partyId && current == generation;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Retire();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}

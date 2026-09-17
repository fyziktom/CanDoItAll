using CanDoItAll.CrmHr.UI.Financials;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

// Owns the Financials read for one host instance: the account it serves, the single accepted snapshot per read,
// the Loading/Ready/Failed phases, retry admission, the period projection and the fencing of late outcomes. The
// query owner and its recognition rules stay behind ICrmFinancialSnapshotQueryService; the session never renders.
public sealed class CrmFinancialsReadSession : IDisposable
{
    public const string FailureMessage = "The financial projection is unavailable. Retry after checking the CRM data source.";
    public const string AccountRequiredMessage = "Choose an account to load its commercial results.";

    private readonly ICrmFinancialSnapshotQueryService query;
    private readonly ILogger? logger;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? read;
    private CrmHrFinancialPeriod period = CrmHrFinancialPeriod.Month;
    private long generation;
    private bool hasTarget;
    private bool disposed;

    public CrmFinancialsReadSession(ICrmFinancialSnapshotQueryService query, ILogger? logger = null)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.logger = logger;
        Presentation = CrmHrFinancialsPresentation.CreateLoading(generation, period);
    }

    // The account this instance serves; Guid.Empty until the host named one.
    public Guid Target { get; private set; }

    public CrmHrFinancialsPresentation Presentation { get; private set; }

    // Raised after every accepted presentation change so the host can render; never raised after disposal.
    public Func<Task>? Changed { get; set; }

    // The host's account. A same-account echo issues no read and never replays a failed one (retry is explicit).
    // Another account retires the read in flight and starts its own, so a late outcome of the previous account is
    // never shown as this one. An empty identifier fails at once without touching the query owner.
    public Task LoadAsync(Guid accountPartyId)
    {
        if (disposed || (hasTarget && accountPartyId == Target))
        {
            return Task.CompletedTask;
        }

        hasTarget = true;
        Target = accountPartyId;
        Retire();
        var current = generation;
        if (accountPartyId == Guid.Empty)
        {
            Presentation = CrmHrFinancialsPresentation.CreateFailed(current, AccountRequiredMessage, period);
            return PublishAsync(current);
        }

        return ReadAsync(accountPartyId, current, retrying: false);
    }

    // Reads the current failed account again. Stale generations, duplicate admissions while a read is in flight
    // and retries of an accepted or loading account are ignored.
    public Task RetryAsync(long requested)
    {
        if (disposed || Target == Guid.Empty || requested != generation || Presentation.Phase != CrmHrFinancialsPhase.Failed || Presentation.IsRetrying)
        {
            return Task.CompletedTask;
        }

        Retire();
        return ReadAsync(Target, generation, retrying: true);
    }

    // A local projection change: the accepted snapshot is shown by another period and no read is issued.
    public void SetPeriod(CrmHrFinancialPeriod value)
    {
        if (disposed || period == value)
        {
            return;
        }

        period = value;
        Presentation = Presentation with { Period = value };
    }

    private async Task ReadAsync(Guid accountPartyId, long current, bool retrying)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        read = source;
        Presentation = retrying
            ? CrmHrFinancialsPresentation.CreateFailed(current, Presentation.FailureMessage ?? FailureMessage, period, isRetrying: true)
            : CrmHrFinancialsPresentation.CreateLoading(current, period);
        await PublishAsync(current);
        try
        {
            var snapshot = await query.GetAsync(accountPartyId, source.Token);
            if (!IsCurrent(accountPartyId, current))
            {
                return;
            }

            if (snapshot.AccountPartyId != accountPartyId)
            {
                // A snapshot for another account is never accepted, whatever completed last.
                logger?.LogError(
                    "The CRM financial snapshot for account {AccountPartyId} answered with account {ReturnedAccountPartyId}.",
                    accountPartyId,
                    snapshot.AccountPartyId);
                Presentation = CrmHrFinancialsPresentation.CreateFailed(current, FailureMessage, period);
            }
            else
            {
                Presentation = CrmHrFinancialsPresentation.CreateReady(current, CrmFinancialsPresentationMapper.ToSnapshot(snapshot), period);
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(accountPartyId, current))
            {
                return;
            }

            // The user gets safe copy; the exception goes to the log only.
            logger?.LogError(exception, "CRM financial snapshot load failed for account {AccountPartyId}.", accountPartyId);
            Presentation = CrmHrFinancialsPresentation.CreateFailed(current, FailureMessage, period);
        }
        finally
        {
            if (ReferenceEquals(read, source))
            {
                read = null;
            }

            // The query has returned, so nothing can still need this token source.
            source.Dispose();
        }

        await PublishAsync(current);
    }

    // Cancels the read in flight and advances the generation, so its late success, failure or completion is inert.
    private void Retire()
    {
        generation++;
        var retired = read;
        read = null;
        retired?.Cancel();
    }

    private bool IsCurrent(Guid accountPartyId, long current)
        => !disposed && Target == accountPartyId && current == generation;

    private async Task PublishAsync(long current)
    {
        if (!IsCurrent(Target, current))
        {
            return;
        }

        try
        {
            await (Changed?.Invoke() ?? Task.CompletedTask);
        }
        catch (Exception exception) when (!lifetime.IsCancellationRequested)
        {
            // A render failure never changes what is known about the read.
            logger?.LogWarning(exception, "The CRM financial snapshot could not be rendered.");
        }
    }

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

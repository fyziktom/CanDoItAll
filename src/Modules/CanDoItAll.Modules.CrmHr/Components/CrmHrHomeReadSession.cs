using CanDoItAll.CrmHr.UI.Home;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

// Owns the Home overview read for one host instance: the single accepted snapshot, the Loading/Ready/Failed
// phases, retry admission and the fencing of late results. It never navigates, publishes agent context or renders.
public sealed class CrmHrHomeReadSession : IDisposable
{
    public const string FailureMessage = "The CRM / HR overview could not be loaded. Retry, or open the directory directly.";

    private readonly ICrmHrHomeQueryService query;
    private readonly ILogger? logger;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private bool isReading;
    private bool disposed;

    public CrmHrHomeReadSession(ICrmHrHomeQueryService query, ILogger? logger = null)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.logger = logger;
        Presentation = CrmHrHomePresentation.CreateLoading(generation);
    }

    public CrmHrHomePresentation Presentation { get; private set; }

    // Raised after every accepted presentation change so the host can render; never raised after disposal.
    public Func<Task>? Changed { get; set; }

    // The first read. A same-instance rerender must not call this again; the host calls it once from initialization.
    public Task LoadAsync()
    {
        if (disposed || isReading || Presentation.Phase == CrmHrHomePhase.Ready)
        {
            return Task.CompletedTask;
        }

        return ReadAsync(++generation, retrying: false);
    }

    // Explicit retry of a failed read. Stale generations and duplicate admissions while a read is in flight are ignored.
    public Task RetryAsync(long requested)
    {
        if (disposed || isReading || requested != generation || Presentation.Phase != CrmHrHomePhase.Failed)
        {
            return Task.CompletedTask;
        }

        return ReadAsync(++generation, retrying: true);
    }

    private async Task ReadAsync(long current, bool retrying)
    {
        isReading = true;
        Presentation = retrying
            ? CrmHrHomePresentation.CreateFailed(current, Presentation.FailureMessage ?? FailureMessage, isRetrying: true)
            : CrmHrHomePresentation.CreateLoading(current);
        await PublishAsync(current);
        try
        {
            var snapshot = await query.GetAsync(lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            Presentation = CrmHrHomePresentation.CreateReady(current, CrmHrHomePresentationMapper.ToOverview(snapshot));
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(current))
            {
                return;
            }

            // The user gets safe copy; the exception goes to the log only.
            logger?.LogError(exception, "The CRM / HR Home overview read failed.");
            Presentation = CrmHrHomePresentation.CreateFailed(current, FailureMessage);
        }
        finally
        {
            if (IsCurrent(current))
            {
                isReading = false;
            }
        }

        await PublishAsync(current);
    }

    private bool IsCurrent(long requested) => !disposed && requested == generation;

    private async Task PublishAsync(long current)
    {
        if (!IsCurrent(current))
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
            logger?.LogWarning(exception, "The CRM / HR Home overview could not be rendered.");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}

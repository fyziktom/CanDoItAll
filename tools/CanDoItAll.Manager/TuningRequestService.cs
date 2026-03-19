using System.Threading.Channels;

namespace CanDoItAll.Manager;

public enum TuningRequestStatus
{
    Queued,
    Packaging,
    AwaitingApproval,
    SubmittedToCodex,
    CodexRunning,
    ChangesApplied,
    WaitingForWatchReady,
    ReadyForReview,
    VerificationPassed,
    VerificationFailed,
    Failed,
    Cancelled
}

public sealed record TuningRequestCreateModel(
    string CapsuleKey,
    string ComponentName,
    string Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId,
    string Instruction,
    bool AutoSubmit = false);

public sealed record TuningRequestRecord(
    Guid Id,
    string CorrelationId,
    string CapsuleKey,
    string ComponentName,
    string Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId,
    string Instruction,
    TuningRequestStatus Status,
    DateTimeOffset CreatedAtUtc,
    string Summary,
    long? ReadyWatchEventId,
    int? ReadyWatchIteration,
    bool CapsuleDriftDetected);

public sealed record TuningRequestEvent(Guid RequestId, DateTimeOffset TimestampUtc, TuningRequestStatus Status, string Summary);

/* codex-capsule
kind: service
name: TuningRequestService
summary: Stores dev-only tuning requests and advances them through a controlled fake execution lifecycle.
owns: tuning-requests, tuning-events
deps: ManagerOptions, IWatchSupervisor, ICapsuleCatalogService
risks: auto-submit-without-ready, missing-capsule
tests: unit:TuningRequestServiceTests
inputs: TuningRequestCreateModel
outputs: TuningRequestRecord, TuningRequestEvent stream
*/
public sealed class TuningRequestService(
    IConfiguration configuration,
    IWatchSupervisor watchSupervisor,
    ICapsuleCatalogService capsuleCatalogService)
{
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    private readonly Dictionary<Guid, TuningRequestRecord> _requests = [];
    private readonly Dictionary<Guid, List<TuningRequestEvent>> _events = [];
    private readonly Dictionary<Guid, EventStreamHub<TuningRequestEvent>> _eventHubs = [];
    private readonly object _gate = new();

    public TuningRequestRecord? Get(Guid id)
    {
        lock (_gate)
        {
            return _requests.GetValueOrDefault(id);
        }
    }

    public ChannelReader<TuningRequestEvent> Subscribe(Guid requestId, out Guid subscriptionId)
    {
        lock (_gate)
        {
            if (!_eventHubs.TryGetValue(requestId, out var hub))
            {
                hub = new EventStreamHub<TuningRequestEvent>();
                _eventHubs[requestId] = hub;
            }

            return hub.Subscribe(out subscriptionId);
        }
    }

    public void Unsubscribe(Guid requestId, Guid subscriptionId)
    {
        lock (_gate)
        {
            if (_eventHubs.TryGetValue(requestId, out var hub))
            {
                hub.Unsubscribe(subscriptionId);
            }
        }
    }

    public async Task<TuningRequestRecord> CreateAsync(TuningRequestCreateModel model, CancellationToken cancellationToken = default)
    {
        if (!_options.TuningModeEnabled)
        {
            throw new InvalidOperationException("Tuning mode is disabled.");
        }

        if (string.IsNullOrWhiteSpace(model.Instruction))
        {
            throw new InvalidOperationException("The tuning request instruction is required.");
        }

        if (capsuleCatalogService.GetSymbol(model.CapsuleKey) is null)
        {
            throw new InvalidOperationException("The requested capsule key was not found in the current capsule catalog.");
        }

        var record = new TuningRequestRecord(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            model.CapsuleKey,
            model.ComponentName.Trim(),
            string.IsNullOrWhiteSpace(model.Route) ? "/" : model.Route.Trim(),
            model.ProjectId,
            model.TabId,
            model.SelectionId,
            model.Instruction.Trim(),
            _options.ReviewBeforeSend && !model.AutoSubmit ? TuningRequestStatus.AwaitingApproval : TuningRequestStatus.Queued,
            DateTimeOffset.UtcNow,
            "Tuning request created.",
            null,
            null,
            false);

        lock (_gate)
        {
            _requests[record.Id] = record;
            _events[record.Id] = [];
            _eventHubs[record.Id] = new EventStreamHub<TuningRequestEvent>();
        }

        await PublishAsync(record.Id, record.Status, record.Summary, cancellationToken);
        if (!_options.ReviewBeforeSend || model.AutoSubmit)
        {
            _ = Task.Run(() => SimulateExecutionAsync(record.Id, cancellationToken), cancellationToken);
        }

        return record;
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SetStatusAsync(id, TuningRequestStatus.Cancelled, "Tuning request cancelled.", null, null, false, cancellationToken);
    }

    private async Task SimulateExecutionAsync(Guid id, CancellationToken cancellationToken)
    {
        await SetStatusAsync(id, TuningRequestStatus.Packaging, "Packaging request payload.", null, null, false, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await SetStatusAsync(id, TuningRequestStatus.SubmittedToCodex, "Submitted to Codex adapter.", null, null, false, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await SetStatusAsync(id, TuningRequestStatus.CodexRunning, "Codex is applying changes.", null, null, false, cancellationToken);
        await Task.Delay(150, cancellationToken);
        await SetStatusAsync(id, TuningRequestStatus.ChangesApplied, "Changes applied, waiting for watch readiness.", null, null, false, cancellationToken);
        await SetStatusAsync(id, TuningRequestStatus.WaitingForWatchReady, "Waiting for the watched app to become ready.", null, null, false, cancellationToken);

        var ready = await watchSupervisor.WaitForReadyAsync(0, TimeSpan.FromSeconds(30), cancellationToken);
        if (ready is null)
        {
            await SetStatusAsync(id, TuningRequestStatus.VerificationFailed, "The watched app did not become ready in time.", null, null, false, cancellationToken);
            return;
        }

        var coverage = capsuleCatalogService.GetCoverage();
        if (coverage.HasDrift)
        {
            await SetStatusAsync(
                id,
                TuningRequestStatus.VerificationFailed,
                $"Capsule drift detected after changes: {coverage.MissingFiles} missing, {coverage.MalformedFiles} malformed.",
                ready.LastEventId,
                ready.ConfirmedWatchIteration ?? ready.ExpectedWatchIteration,
                true,
                cancellationToken);
            return;
        }

        await SetStatusAsync(
            id,
            TuningRequestStatus.ReadyForReview,
            "The request is ready for review.",
            ready.LastEventId,
            ready.ConfirmedWatchIteration ?? ready.ExpectedWatchIteration,
            false,
            cancellationToken);
    }

    private async Task SetStatusAsync(
        Guid id,
        TuningRequestStatus status,
        string summary,
        long? readyWatchEventId,
        int? readyWatchIteration,
        bool capsuleDriftDetected,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
            {
                return;
            }

            _requests[id] = record with
            {
                Status = status,
                Summary = summary,
                ReadyWatchEventId = readyWatchEventId ?? record.ReadyWatchEventId,
                ReadyWatchIteration = readyWatchIteration ?? record.ReadyWatchIteration,
                CapsuleDriftDetected = capsuleDriftDetected
            };
        }

        await PublishAsync(id, status, summary, cancellationToken);
    }

    private async Task PublishAsync(Guid id, TuningRequestStatus status, string summary, CancellationToken cancellationToken)
    {
        TuningRequestEvent tuningEvent;
        EventStreamHub<TuningRequestEvent>? hub;
        lock (_gate)
        {
            tuningEvent = new TuningRequestEvent(id, DateTimeOffset.UtcNow, status, summary);
            _events[id].Add(tuningEvent);
            hub = _eventHubs.GetValueOrDefault(id);
        }

        if (hub is not null)
        {
            await hub.PublishAsync(tuningEvent, cancellationToken);
        }
    }
}

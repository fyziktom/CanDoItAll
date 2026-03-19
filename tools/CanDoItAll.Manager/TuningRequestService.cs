using System.Text.Json;
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

public sealed record TuningRequestAttachmentCreateModel(
    string FileName,
    string ContentType,
    string ContentBase64,
    string Source);

public sealed record TuningRequestCreateModel(
    string CapsuleKey,
    string ComponentName,
    string Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId,
    string? ContextSummary,
    string Instruction,
    IReadOnlyList<TuningRequestAttachmentCreateModel>? Attachments = null,
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
    string? ContextSummary,
    string Instruction,
    TuningRequestStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Summary,
    long? ReadyWatchEventId,
    int? ReadyWatchIteration,
    bool CapsuleDriftDetected,
    int AttachmentCount,
    string CapsuleSummary,
    string EvidenceDirectory,
    string? AdapterJobId);

public sealed record TuningRequestEvent(Guid RequestId, DateTimeOffset TimestampUtc, TuningRequestStatus Status, string Summary);

/* codex-capsule
kind: service
name: TuningRequestService
summary: Stores development tuning requests, packages evidence, and runs them through a real local adapter with watch-ready verification.
owns: tuning-requests, tuning-events, request-packets, evidence-paths
deps: ManagerOptions, IWatchSupervisor, ICapsuleCatalogService, ITuningExecutionAdapter
risks: missing-adapter-config, unsafe-attachment-size, watch-ready-timeout
tests: unit:TuningRequestServiceTests
inputs: TuningRequestCreateModel
outputs: TuningRequestRecord, TuningRequestEvent stream
*/
public sealed class TuningRequestService(
    IConfiguration configuration,
    IWatchSupervisor watchSupervisor,
    ICapsuleCatalogService capsuleCatalogService,
    ITuningExecutionAdapter tuningExecutionAdapter)
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

        var capsule = capsuleCatalogService.GetSymbol(model.CapsuleKey)
            ?? throw new InvalidOperationException("The requested capsule key was not found in the current capsule catalog.");

        var requestId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var requestDirectory = CreateRequestDirectory(requestId);
        var attachments = await PersistAttachmentsAsync(requestDirectory, model.Attachments ?? [], cancellationToken);
        await WritePacketAsync(requestDirectory, new TuningRequestPacket(
            requestId,
            correlationId,
            model.CapsuleKey,
            model.ComponentName.Trim(),
            string.IsNullOrWhiteSpace(model.Route) ? "/" : model.Route.Trim(),
            model.ProjectId,
            model.TabId,
            model.SelectionId,
            model.ContextSummary?.Trim(),
            model.Instruction.Trim(),
            attachments,
            capsule.Summary,
            createdAtUtc), cancellationToken);

        var initialStatus = _options.ReviewBeforeSend && !model.AutoSubmit
            ? TuningRequestStatus.AwaitingApproval
            : TuningRequestStatus.Queued;
        var record = new TuningRequestRecord(
            requestId,
            correlationId,
            model.CapsuleKey,
            model.ComponentName.Trim(),
            string.IsNullOrWhiteSpace(model.Route) ? "/" : model.Route.Trim(),
            model.ProjectId,
            model.TabId,
            model.SelectionId,
            model.ContextSummary?.Trim(),
            model.Instruction.Trim(),
            initialStatus,
            createdAtUtc,
            createdAtUtc,
            initialStatus == TuningRequestStatus.AwaitingApproval
                ? "Tuning request packaged and waiting for approval."
                : "Tuning request created.",
            null,
            null,
            false,
            attachments.Count,
            capsule.Summary,
            requestDirectory,
            null);

        lock (_gate)
        {
            _requests[record.Id] = record;
            _events[record.Id] = [];
            _eventHubs[record.Id] = new EventStreamHub<TuningRequestEvent>();
        }

        await PublishAsync(record.Id, record.Status, record.Summary, cancellationToken);
        await AppendEventLogAsync(record, cancellationToken);
        if (record.Status == TuningRequestStatus.Queued)
        {
            _ = Task.Run(() => RunExecutionAsync(record.Id, CancellationToken.None), CancellationToken.None);
        }

        return record;
    }

    public async Task<TuningRequestRecord> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = Get(id) ?? throw new InvalidOperationException("The requested tuning request was not found.");
        if (current.Status != TuningRequestStatus.AwaitingApproval)
        {
            return current;
        }

        await SetStatusAsync(id, TuningRequestStatus.Queued, "Tuning request approved and queued for the local adapter.", null, null, current.CapsuleDriftDetected, current.AdapterJobId, cancellationToken);
        _ = Task.Run(() => RunExecutionAsync(id, CancellationToken.None), CancellationToken.None);
        return Get(id)!;
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
        => await SetStatusAsync(id, TuningRequestStatus.Cancelled, "Tuning request cancelled.", null, null, false, Get(id)?.AdapterJobId, cancellationToken);

    private async Task RunExecutionAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = Get(id);
        if (record is null || record.Status == TuningRequestStatus.Cancelled)
        {
            return;
        }

        var requestJsonPath = Path.Combine(record.EvidenceDirectory, "request.json");
        var stdoutPath = Path.Combine(record.EvidenceDirectory, "adapter.stdout.log");
        var stderrPath = Path.Combine(record.EvidenceDirectory, "adapter.stderr.log");
        var eventsPath = Path.Combine(record.EvidenceDirectory, "events.ndjson");
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.WorkspaceRoot));

        await SetStatusAsync(id, TuningRequestStatus.Packaging, "Packaging request payload and evidence.", null, null, false, record.AdapterJobId, cancellationToken);

        TuningExecutionResult executionResult;
        try
        {
            await SetStatusAsync(id, TuningRequestStatus.SubmittedToCodex, "Submitted to the local tuning adapter.", null, null, false, record.AdapterJobId, cancellationToken);
            await SetStatusAsync(id, TuningRequestStatus.CodexRunning, "Local tuning adapter is running.", null, null, false, record.AdapterJobId, cancellationToken);
            executionResult = await tuningExecutionAdapter.ExecuteAsync(new TuningExecutionContext(
                id,
                workspaceRoot,
                record.EvidenceDirectory,
                requestJsonPath,
                stdoutPath,
                stderrPath,
                eventsPath), cancellationToken);
        }
        catch (Exception ex)
        {
            await SetStatusAsync(id, TuningRequestStatus.Failed, ex.Message, null, null, false, null, cancellationToken);
            return;
        }

        if (executionResult.ExitCode != 0)
        {
            await SetStatusAsync(id, TuningRequestStatus.Failed, executionResult.Summary, null, null, false, executionResult.AdapterJobId, cancellationToken);
            return;
        }

        await SetStatusAsync(id, TuningRequestStatus.ChangesApplied, executionResult.Summary, null, null, false, executionResult.AdapterJobId, cancellationToken);
        await SetStatusAsync(id, TuningRequestStatus.WaitingForWatchReady, "Waiting for the watched app to become ready.", null, null, false, executionResult.AdapterJobId, cancellationToken);

        var ready = await watchSupervisor.WaitForReadyAsync(0, TimeSpan.FromSeconds(30), cancellationToken);
        if (ready is null)
        {
            await SetStatusAsync(id, TuningRequestStatus.VerificationFailed, "The watched app did not become ready in time.", null, null, false, executionResult.AdapterJobId, cancellationToken);
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
                executionResult.AdapterJobId,
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
            executionResult.AdapterJobId,
            cancellationToken);
    }

    private async Task SetStatusAsync(
        Guid id,
        TuningRequestStatus status,
        string summary,
        long? readyWatchEventId,
        int? readyWatchIteration,
        bool capsuleDriftDetected,
        string? adapterJobId,
        CancellationToken cancellationToken)
    {
        TuningRequestRecord? updatedRecord = null;
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
            {
                return;
            }

            updatedRecord = record with
            {
                Status = status,
                Summary = summary,
                ReadyWatchEventId = readyWatchEventId ?? record.ReadyWatchEventId,
                ReadyWatchIteration = readyWatchIteration ?? record.ReadyWatchIteration,
                CapsuleDriftDetected = capsuleDriftDetected,
                AdapterJobId = adapterJobId ?? record.AdapterJobId,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            _requests[id] = updatedRecord;
        }

        await PublishAsync(id, status, summary, cancellationToken);
        if (updatedRecord is not null)
        {
            await AppendEventLogAsync(updatedRecord, cancellationToken);
        }
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

    private string CreateRequestDirectory(Guid requestId)
    {
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.WorkspaceRoot));
        var artifactsRoot = Path.Combine(workspaceRoot, _options.ArtifactsRoot, "tuning", requestId.ToString("N"));
        Directory.CreateDirectory(artifactsRoot);
        Directory.CreateDirectory(Path.Combine(artifactsRoot, "attachments"));
        return artifactsRoot;
    }

    private async Task<IReadOnlyList<TuningAttachmentPacket>> PersistAttachmentsAsync(
        string requestDirectory,
        IReadOnlyList<TuningRequestAttachmentCreateModel> attachments,
        CancellationToken cancellationToken)
    {
        var saved = new List<TuningAttachmentPacket>();
        var attachmentsDirectory = Path.Combine(requestDirectory, "attachments");
        foreach (var attachment in attachments)
        {
            var bytes = Convert.FromBase64String(attachment.ContentBase64 ?? string.Empty);
            if (bytes.Length > _options.AttachmentSizeLimitBytes)
            {
                throw new InvalidOperationException($"Attachment '{attachment.FileName}' exceeds the configured size limit.");
            }

            var safeName = Path.GetFileName(string.IsNullOrWhiteSpace(attachment.FileName) ? $"{Guid.NewGuid():N}.bin" : attachment.FileName);
            var relativePath = Path.Combine("attachments", safeName);
            var fullPath = Path.Combine(requestDirectory, relativePath);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
            saved.Add(new TuningAttachmentPacket(safeName, attachment.ContentType, attachment.Source, relativePath.Replace('\\', '/')));
        }

        return saved;
    }

    private static async Task WritePacketAsync(string requestDirectory, TuningRequestPacket packet, CancellationToken cancellationToken)
    {
        var packetPath = Path.Combine(requestDirectory, "request.json");
        await File.WriteAllTextAsync(
            packetPath,
            JsonSerializer.Serialize(packet, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            cancellationToken);
    }

    private static async Task AppendEventLogAsync(TuningRequestRecord record, CancellationToken cancellationToken)
    {
        var logPath = Path.Combine(record.EvidenceDirectory, "events.ndjson");
        var entry = new TuningEventLogEntry(
            record.UpdatedAtUtc,
            record.Status.ToString(),
            record.Summary,
            record.AdapterJobId);
        await File.AppendAllTextAsync(logPath, $"{JsonSerializer.Serialize(entry)}{Environment.NewLine}", cancellationToken);
    }
}

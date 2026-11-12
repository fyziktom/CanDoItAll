using System.Globalization;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Infrastructure;

public sealed class BrowserProjectPendingSubmissionStore(
    IJSRuntime jsRuntime,
    IProjectDefinitionRecoveryCodec codec,
    ProjectPendingStoreLimits limits) : IProjectPendingSubmissionStore, IProjectPendingReferenceStore, IAsyncDisposable {
    private const string ModulePath = "./js/projectPendingSubmissions.js";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };
    private readonly SemaphoreSlim initialization = new(1, 1);
    private IJSObjectReference? module;
    private bool ready;
    private bool disposed;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default) {
        await initialization.WaitAsync(cancellationToken);
        try {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ready) {
                return;
            }
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            var result = await module.InvokeAsync<WireInitialization>("initialize", cancellationToken, new {
                limits.MaximumRecordCount,
                limits.MaximumTotalBytes,
                maximumFrozenRequestBytes = limits.Definition.MaxRawRequestBytes,
                maximumAcceptedBytes = limits.Definition.MaxAcceptedResultBytes,
                limits.MaximumPageSize
            });
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (result.Schema != 1) {
                throw new InvalidOperationException("The pending store schema is unsupported.");
            }
            ready = true;
        } finally {
            initialization.Release();
        }
    }

    public ValueTask<ProjectPendingStoreChange> PrepareAsync(ProjectDefinitionFrozenSubmission frozen, bool hadStarterPlan,
        CancellationToken cancellationToken = default) {
        ValidateFrozen(frozen);
        return ChangeAsync("prepare", frozen.Submission.Operation, cancellationToken, new {
            key = Key(frozen.Submission.Operation), frozenRequest = frozen.Payload,
            targetProjectId = frozen.Submission.TargetProjectId, hadStarterPlan
        });
    }

    public ValueTask<ProjectPendingStoreChange> BeginDispatchAsync(ProjectPendingSubmission record, Guid attemptId,
        bool explicitRetry, CancellationToken cancellationToken = default) {
        if (attemptId == Guid.Empty) {
            throw new ArgumentException("A dispatch attempt must have an identity.", nameof(attemptId));
        }
        return ChangeAsync("beginDispatch", record.Operation, cancellationToken,
            Key(record.Operation), record.Revision.Value, attemptId.ToString("D"), explicitRetry);
    }

    public ValueTask<ProjectPendingStoreChange> ObserveAcceptedAsync(ProjectDefinitionFrozenSubmission frozen,
        ProjectDefinitionAcceptedReceipt receipt, CancellationToken cancellationToken = default) {
        ValidateFrozen(frozen);
        var encoded = codec.EncodeAccepted(frozen.Submission, receipt, limits.Definition);
        return ChangeAsync("observeAccepted", frozen.Submission.Operation, cancellationToken, Key(frozen.Submission.Operation), encoded.Payload);
    }

    public ValueTask<ProjectPendingStoreChange> ObserveAttemptAsync(ProjectDefinitionOperation operation, Guid attemptId,
        ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) {
        if (attemptId == Guid.Empty) {
            throw new ArgumentException("A dispatch attempt must have an identity.", nameof(attemptId));
        }
        var value = outcome switch {
            ProjectPendingAttemptOutcome.Rejected => "rejected",
            ProjectPendingAttemptOutcome.Unconfirmed => "unconfirmed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
        return ChangeAsync("observeAttempt", operation, cancellationToken, Key(operation), attemptId.ToString("D"), value);
    }

    public ValueTask<ProjectPendingStoreChange> ObserveClosedAbsentAsync(ProjectDefinitionFrozenSubmission frozen,
        ProjectDefinitionLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) {
        ValidateAbsence(absence, frozen.Submission.Operation);
        return ChangeAsync("observeClosedAbsent", frozen.Submission.Operation, cancellationToken,
            Key(frozen.Submission.Operation), JsonSerializer.Serialize(absence, Json));
    }

    public async ValueTask<ProjectPendingSubmission?> ReadAsync(ProjectDefinitionOperation operation, CancellationToken cancellationToken = default)
        => ReadRecord(await InvokeAsync<WireRecord?>("read", cancellationToken, Key(operation)), operation);

    public async ValueTask<ProjectPendingSubmissionPage> ListAsync(ProjectPendingScope scope, int take,
        ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default) {
        if (take <= 0 || take > limits.MaximumPageSize || after is not null && !scope.Contains(after)) {
            throw new ArgumentOutOfRangeException(nameof(take), "The pending page or continuation is outside its supported scope.");
        }
        var page = await InvokeAsync<WirePage>("list", cancellationToken,
            new { @namespace = scope.Namespace.Value.ToString("D"), callerBinding = scope.CallerBinding.Value }, take, after is null ? null : Key(after));
        if (page.Items is null || page.Items.Length > take) {
            throw new InvalidOperationException("The pending store returned an invalid page.");
        }
        var items = page.Items.Select(item => {
            var operation = Operation(item.Key);
            if (!scope.Contains(operation) || item.TargetProjectId == Guid.Empty) {
                throw new InvalidOperationException("The pending store returned metadata outside its requested scope.");
            }
            return new ProjectPendingSubmissionMetadata(operation, new(item.Revision), Stage(item.Stage), item.TargetProjectId,
                item.HadUnconfirmedDispatch, item.HadStarterPlan);
        }).ToArray();
        var next = page.NextKey is null ? null : Operation(page.NextKey);
        if (next is not null && (!scope.Contains(next) || items.Length == 0 || next != items[^1].Operation)) {
            throw new InvalidOperationException("The pending store returned an invalid continuation.");
        }
        return new(items, next);
    }

    public ValueTask<ProjectPendingStoreChange> CancelPreparedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default)
        => ChangeAsync("cancelPrepared", record.Operation, cancellationToken, Key(record.Operation), record.Revision.Value);

    public ValueTask<ProjectPendingStoreChange> ClearResolvedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default)
        => ChangeAsync("clearResolved", record.Operation, cancellationToken, Key(record.Operation), record.Revision.Value);

    public async ValueTask<ProjectPendingReferencePage> ListReferencesAsync(int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default) {
        if (take <= 0 || take > limits.MaximumPageSize) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }
        var page = await InvokeAsync<WireReferencePage>("listReferences", cancellationToken, take, after is null ? null : Key(after));
        if (page.Items.Length > take || page.Count < 0 || page.Count > ProjectPendingRevision.Maximum ||
            page.Bytes < 0 || page.Bytes > ProjectPendingRevision.Maximum) {
            throw new InvalidOperationException("The retained-reference inventory has invalid bounds.");
        }
        var items = Array.AsReadOnly(page.Items.Select(ReadReference).ToArray());
        var next = page.NextKey is null ? null : Operation(page.NextKey);
        if (items.Select(item => item.Operation).Distinct().Count() != items.Count || next is not null && (items.Count == 0 || next != items[^1].Operation)) {
            throw new InvalidOperationException("The retained-reference page has an invalid continuation.");
        }
        return new(items, next, page.Count, page.Bytes);
    }

    public async ValueTask<ProjectPendingReferenceChange> DiscardEvidenceAsync(ProjectPendingReference reference, CancellationToken cancellationToken = default) {
        var change = await InvokeAsync<WireReferenceChange>("discardEvidence", cancellationToken, Key(reference.Operation), reference.Revision.Value);
        var status = change.Status switch {
            "applied" => ProjectPendingStoreStatus.Applied,
            "changed" => ProjectPendingStoreStatus.Changed,
            "missing" => ProjectPendingStoreStatus.Missing,
            _ => throw new InvalidOperationException("The reference disposal result is unsupported.")
        };
        var current = change.Reference is null ? null : ReadReference(change.Reference);
        if ((status == ProjectPendingStoreStatus.Changed) != (current is not null) || current is not null && current.Operation != reference.Operation) {
            throw new InvalidOperationException("The reference disposal result is inconsistent.");
        }
        return new(status, current);
    }

    private static ProjectPendingReference ReadReference(WireReference value) {
        if (value.PayloadBytes <= 0 || value.PayloadBytes > ProjectPendingRevision.Maximum || value.TargetProjectId == Guid.Empty) {
            throw new InvalidOperationException("The retained-reference metadata is invalid.");
        }
        return new(Operation(value.Key), new(value.Revision), Stage(value.Stage), value.TargetProjectId,
            value.HadUnconfirmedDispatch, value.HadStarterPlan, value.PayloadBytes);
    }

    private async ValueTask<ProjectPendingStoreChange> ChangeAsync(string method, ProjectDefinitionOperation operation,
        CancellationToken cancellationToken, params object?[] args) {
        var change = await InvokeAsync<WireChange>(method, cancellationToken, args);
        var status = change.Status switch {
            "applied" => ProjectPendingStoreStatus.Applied,
            "unchanged" => ProjectPendingStoreStatus.Unchanged,
            "changed" => ProjectPendingStoreStatus.Changed,
            "missing" => ProjectPendingStoreStatus.Missing,
            _ => throw new InvalidOperationException("The pending store returned an unknown transition result.")
        };
        var record = ReadRecord(change.Record, operation);
        if (status == ProjectPendingStoreStatus.Missing && record is not null ||
            status is ProjectPendingStoreStatus.Changed or ProjectPendingStoreStatus.Unchanged && record is null ||
            status == ProjectPendingStoreStatus.Applied && record is null && method is not "clearResolved" and not "cancelPrepared") {
            throw new InvalidOperationException("The pending store returned an inconsistent transition result.");
        }
        return new(status, record);
    }

    private ProjectPendingSubmission? ReadRecord(WireRecord? value, ProjectDefinitionOperation expected) {
        if (value is null) {
            return null;
        }
        var operation = Operation(value.Key);
        if (value.Schema != 1 || operation != expected) {
            throw new InvalidOperationException("The pending record schema or operation address is invalid.");
        }
        var frozen = codec.ReadSubmission(value.FrozenRequest, limits.Definition);
        if (frozen.Submission.Operation != operation || frozen.Submission.TargetProjectId != value.TargetProjectId) {
            throw new InvalidOperationException("The pending command does not match its record address or target.");
        }
        var stage = Stage(value.Stage);
        var accepted = value.AcceptedPayload is null ? null : codec.ReadAccepted(frozen.Submission, value.AcceptedPayload, limits.Definition);
        ProjectDefinitionLookup.ClosedAbsent? absence = null;
        if (value.AbsenceProof is not null) {
            if (value.AbsenceProof.Length > limits.Definition.MaxAcceptedResultBytes ||
                Encoding.UTF8.GetByteCount(value.AbsenceProof) > limits.Definition.MaxAcceptedResultBytes) {
                throw new InvalidOperationException("The pending absence proof exceeds its byte bound.");
            }
            absence = JsonSerializer.Deserialize<ProjectDefinitionLookup.ClosedAbsent>(value.AbsenceProof, Json)
                ?? throw new JsonException("The pending absence proof is missing.");
            ValidateAbsence(absence, operation);
        }
        if ((stage == ProjectPendingStage.Accepted) != (accepted is not null) ||
            (stage == ProjectPendingStage.ClosedAbsent) != (absence is not null) ||
            (stage == ProjectPendingStage.Prepared) != (value.AttemptId is null) || value.AttemptId == Guid.Empty ||
            stage == ProjectPendingStage.Unconfirmed && !value.HadUnconfirmedDispatch ||
            stage == ProjectPendingStage.Prepared && value.HadUnconfirmedDispatch ||
            stage == ProjectPendingStage.Rejected && value.HadUnconfirmedDispatch) {
            throw new InvalidOperationException("The pending record has inconsistent protocol state.");
        }
        return new(frozen, new(value.Revision), stage, value.AttemptId, value.HadUnconfirmedDispatch, value.HadStarterPlan, accepted, absence);
    }

    private void ValidateFrozen(ProjectDefinitionFrozenSubmission frozen) {
        if (!StringComparer.Ordinal.Equals(codec.Freeze(frozen.Submission, limits.Definition).Payload, frozen.Payload)) {
            throw new InvalidOperationException("The pending command differs from its frozen payload.");
        }
    }

    private static void ValidateAbsence(ProjectDefinitionLookup.ClosedAbsent absence, ProjectDefinitionOperation operation) {
        if (absence.Operation != operation || absence.ClosedAtUtc.Offset != TimeSpan.Zero ||
            absence.ObservedAtUtc.Offset != TimeSpan.Zero || absence.RetainedUntilUtc.Offset != TimeSpan.Zero ||
            absence.ClosedAtUtc > absence.ObservedAtUtc || absence.ClosedAtUtc >= absence.RetainedUntilUtc) {
            throw new InvalidOperationException("The closed absence does not match the pending operation or its retained observation interval.");
        }
    }

    private async ValueTask<T> InvokeAsync<T>(string method, CancellationToken cancellationToken, params object?[] args) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ready || module is null) {
            throw new InvalidOperationException("Initialize the pending store after interactive browser startup.");
        }
        var result = await module.InvokeAsync<T>(method, cancellationToken, args);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        return result;
    }

    private static WireKey Key(ProjectDefinitionOperation operation)
        => new(operation.Namespace.Value.ToString("D"), operation.CallerBinding.Value,
            operation.Epoch.Value.ToString(CultureInfo.InvariantCulture), operation.Id.ToString("D"));

    private static ProjectDefinitionOperation Operation(WireKey key) {
        if (!long.TryParse(key.Epoch, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0 ||
            epoch.ToString(CultureInfo.InvariantCulture) != key.Epoch ||
            !Guid.TryParseExact(key.Namespace, "D", out var dataset) || dataset.ToString("D") != key.Namespace ||
            !Guid.TryParseExact(key.Id, "D", out var id) || id.ToString("D") != key.Id) {
            throw new InvalidOperationException("The pending operation address is invalid.");
        }
        return new(new(dataset), new(epoch), id, new(key.CallerBinding));
    }

    private static ProjectPendingStage Stage(string value) => value switch {
        "prepared" => ProjectPendingStage.Prepared,
        "dispatching" => ProjectPendingStage.Dispatching,
        "unconfirmed" => ProjectPendingStage.Unconfirmed,
        "accepted" => ProjectPendingStage.Accepted,
        "rejected" => ProjectPendingStage.Rejected,
        "closedAbsent" => ProjectPendingStage.ClosedAbsent,
        _ => throw new InvalidOperationException("The pending record stage is unsupported.")
    };

    public async ValueTask DisposeAsync() {
        disposed = true;
        ready = false;
        await initialization.WaitAsync();
        try {
            if (module is { } owned) {
                module = null;
                try {
                    await owned.InvokeVoidAsync("dispose");
                } catch (JSDisconnectedException) {
                } finally {
                    await owned.DisposeAsync();
                }
            }
        } finally {
            initialization.Release();
        }
    }

    private sealed record WireInitialization(int Schema);
    private sealed record WireKey(string Namespace, string CallerBinding, string Epoch, string Id);
    private sealed record WireRecord(int Schema, WireKey Key, long Revision, string Stage, string FrozenRequest,
        Guid? TargetProjectId, bool HadStarterPlan, Guid? AttemptId, bool HadUnconfirmedDispatch, string? AcceptedPayload, string? AbsenceProof);
    private sealed record WireChange(string Status, WireRecord? Record);
    private sealed record WireMetadata(WireKey Key, long Revision, string Stage, Guid? TargetProjectId, bool HadStarterPlan, bool HadUnconfirmedDispatch);
    private sealed record WirePage(WireMetadata[] Items, WireKey? NextKey);
    private sealed record WireReference(WireKey Key, long Revision, string Stage, Guid? TargetProjectId,
        bool HadStarterPlan, bool HadUnconfirmedDispatch, long PayloadBytes);
    private sealed record WireReferencePage(WireReference[] Items, WireKey? NextKey, long Count, long Bytes);
    private sealed record WireReferenceChange(string Status, WireReference? Reference);
}

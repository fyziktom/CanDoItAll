using System.Globalization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Infrastructure;

public sealed class BrowserProjectLifecyclePendingStore(
    IJSRuntime jsRuntime,
    IProjectLifecycleRecoveryCodec codec,
    ProjectLifecyclePendingStoreLimits limits) : IProjectLifecyclePendingStore, IAsyncDisposable {
    private const string ModulePath = "./js/projectLifecyclePendingSubmissions.js";
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
                maximumFrozenRequestBytes = ProjectLifecyclePendingStoreLimits.MaximumSubmissionBytes,
                maximumAcceptedBytes = ProjectLifecyclePendingStoreLimits.MaximumEvidenceBytes,
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

    public ValueTask<ProjectLifecyclePendingChange> PrepareAsync(ProjectLifecycleFrozenSubmission frozen,
        CancellationToken cancellationToken = default) {
        ValidateFrozen(frozen);
        return ChangeAsync("prepare", frozen.Submission.Operation, cancellationToken, new {
            key = Key(frozen.Submission.Operation), frozenRequest = frozen.Payload,
            kind = (int)frozen.Submission.Command.Kind, targetProjectIds = ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command)
        });
    }

    public ValueTask<ProjectLifecyclePendingChange> BeginDispatchAsync(ProjectLifecyclePendingRecord record, Guid attemptId,
        bool explicitRetry, CancellationToken cancellationToken = default) {
        if (attemptId == Guid.Empty) {
            throw new ArgumentException("A dispatch attempt must have an identity.", nameof(attemptId));
        }
        return ChangeAsync("beginDispatch", record.Operation, cancellationToken,
            Key(record.Operation), record.Revision.Value, attemptId.ToString("D"), explicitRetry);
    }

    public ValueTask<ProjectLifecyclePendingChange> ObserveAcceptedAsync(ProjectLifecycleFrozenSubmission frozen,
        ProjectLifecycleAccepted receipt, CancellationToken cancellationToken = default) {
        ValidateFrozen(frozen);
        var encoded = codec.EncodeAccepted(frozen.Submission, receipt);
        return ChangeAsync("observeAccepted", frozen.Submission.Operation, cancellationToken, Key(frozen.Submission.Operation), frozen.Payload, encoded.Payload);
    }

    public ValueTask<ProjectLifecyclePendingChange> ObserveAttemptAsync(ProjectLifecycleOperation operation, Guid attemptId,
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

    public ValueTask<ProjectLifecyclePendingChange> ObserveClosedAbsentAsync(ProjectLifecycleFrozenSubmission frozen,
        ProjectLifecycleLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) {
        ValidateFrozen(frozen);
        var encoded = codec.EncodeClosedAbsent(frozen.Submission, absence);
        return ChangeAsync("observeClosedAbsent", frozen.Submission.Operation, cancellationToken,
            Key(frozen.Submission.Operation), frozen.Payload, encoded.Payload);
    }

    public async ValueTask<ProjectLifecyclePendingRecord?> ReadAsync(ProjectLifecycleOperation operation, CancellationToken cancellationToken = default)
        => ReadRecord(await InvokeAsync<WireRecord?>("read", cancellationToken, Key(operation)), operation);

    public async ValueTask<ProjectLifecyclePendingPage> ListAsync(ProjectLifecyclePendingScope scope, int take,
        ProjectLifecycleOperation? after = null, CancellationToken cancellationToken = default) {
        if (take <= 0 || take > limits.MaximumPageSize || after is not null && !scope.Contains(after)) {
            throw new ArgumentOutOfRangeException(nameof(take), "The pending page or continuation is outside its supported scope.");
        }
        var page = await InvokeAsync<WirePage>("list", cancellationToken,
            new { databaseProfileId = scope.DatabaseProfileId.ToString("D"), @namespace = scope.Namespace.Value.ToString("D"), callerBinding = scope.CallerBinding.Value }, take, after is null ? null : Key(after));
        if (page.Items is null || page.Items.Length > take) {
            throw new InvalidOperationException("The pending store returned an invalid page.");
        }
        var items = page.Items.Select(item => {
            var operation = Operation(item.Key);
            if (!scope.Contains(operation)) {
                throw new InvalidOperationException("The pending store returned metadata outside its requested scope.");
            }
            ValidateTargets(item.Kind, item.TargetProjectIds);
            return new ProjectLifecyclePendingMetadata(operation, new(item.Revision), Stage(item.Stage), (ProjectLifecycleCommandKind)item.Kind,
                item.TargetProjectIds, item.HadUnconfirmedDispatch);
        }).ToArray();
        var next = page.NextKey is null ? null : Operation(page.NextKey);
        if (items.Select(item => item.Operation).Distinct().Count() != items.Length ||
                next is not null && (!scope.Contains(next) || items.Length == 0 || next != items[^1].Operation)) {
            throw new InvalidOperationException("The pending store returned an invalid continuation.");
        }
        return new(items, next);
    }

    public ValueTask<ProjectLifecyclePendingChange> CancelPreparedAsync(ProjectLifecyclePendingRecord record, CancellationToken cancellationToken = default)
        => ChangeAsync("cancelPrepared", record.Operation, cancellationToken, Key(record.Operation), record.Revision.Value);

    public ValueTask<ProjectLifecyclePendingChange> ClearResolvedAsync(ProjectLifecyclePendingRecord record, CancellationToken cancellationToken = default)
        => ChangeAsync("clearResolved", record.Operation, cancellationToken, Key(record.Operation), record.Revision.Value);

    private async ValueTask<ProjectLifecyclePendingChange> ChangeAsync(string method, ProjectLifecycleOperation operation,
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

    private ProjectLifecyclePendingRecord? ReadRecord(WireRecord? value, ProjectLifecycleOperation expected) {
        if (value is null) {
            return null;
        }
        var operation = Operation(value.Key);
        if (value.Schema != 1 || operation != expected) {
            throw new InvalidOperationException("The pending record schema or operation address is invalid.");
        }
        var frozen = codec.ReadSubmission(value.FrozenRequest);
        if (frozen.Submission.Operation != operation || (int)frozen.Submission.Command.Kind != value.Kind ||
                !ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command).SequenceEqual(value.TargetProjectIds)) {
            throw new InvalidOperationException("The pending command does not match its record address or target.");
        }
        var stage = Stage(value.Stage);
        var accepted = value.AcceptedPayload is null ? null : codec.ReadAccepted(frozen.Submission, value.AcceptedPayload);
        var absence = value.AbsenceProof is null ? null : codec.ReadClosedAbsent(frozen.Submission, value.AbsenceProof);
        if ((stage == ProjectPendingStage.Accepted) != (accepted is not null) ||
            (stage == ProjectPendingStage.ClosedAbsent) != (absence is not null) ||
            stage == ProjectPendingStage.Prepared && value.AttemptId is not null ||
            stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed or ProjectPendingStage.Rejected && value.AttemptId is null ||
            value.AttemptId == Guid.Empty ||
            stage == ProjectPendingStage.Unconfirmed && !value.HadUnconfirmedDispatch ||
            stage == ProjectPendingStage.Prepared && value.HadUnconfirmedDispatch ||
            stage == ProjectPendingStage.Rejected && value.HadUnconfirmedDispatch) {
            throw new InvalidOperationException("The pending record has inconsistent protocol state.");
        }
        return new(frozen, new(value.Revision), stage, value.AttemptId, value.HadUnconfirmedDispatch, accepted, absence);
    }

    private void ValidateFrozen(ProjectLifecycleFrozenSubmission frozen) {
        if (!StringComparer.Ordinal.Equals(codec.Freeze(frozen.Submission).Payload, frozen.Payload)) {
            throw new InvalidOperationException("The pending command differs from its frozen payload.");
        }
    }

    private static void ValidateTargets(int kind, Guid[] targets) {
        var expected = (ProjectLifecycleCommandKind)kind switch {
            ProjectLifecycleCommandKind.Delete => 1,
            ProjectLifecycleCommandKind.EnsureLink or ProjectLifecycleCommandKind.RemoveLink => 2,
            ProjectLifecycleCommandKind.ReconnectLink => 3,
            _ => throw new InvalidOperationException("The pending lifecycle kind is unsupported.")
        };
        if (targets is null || targets.Length != expected || targets.Any(value => value == Guid.Empty) ||
                targets.Distinct().Count() != expected || !targets.SequenceEqual(targets.OrderBy(value => value.ToString("D"), StringComparer.Ordinal))) {
            throw new InvalidOperationException("The pending lifecycle targets are invalid.");
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

    private static WireKey Key(ProjectLifecycleOperation operation)
        => new(operation.DatabaseProfileId.ToString("D"), operation.Namespace.Value.ToString("D"), operation.CallerBinding.Value,
            operation.Epoch.Value.ToString(CultureInfo.InvariantCulture), operation.Id.ToString("D"));

    private static ProjectLifecycleOperation Operation(WireKey key) {
        if (!Guid.TryParseExact(key.DatabaseProfileId, "D", out var profile) || profile.ToString("D") != key.DatabaseProfileId ||
            !long.TryParse(key.Epoch, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch <= 0 ||
            epoch.ToString(CultureInfo.InvariantCulture) != key.Epoch ||
            !Guid.TryParseExact(key.Namespace, "D", out var dataset) || dataset.ToString("D") != key.Namespace ||
            !Guid.TryParseExact(key.Id, "D", out var id) || id.ToString("D") != key.Id) {
            throw new InvalidOperationException("The pending operation address is invalid.");
        }
        return new(profile, new(dataset), new(epoch), new(key.CallerBinding), id);
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
                Exception? primary = null;
                try {
                    await owned.InvokeVoidAsync("dispose");
                } catch (JSDisconnectedException) {
                } catch (Exception failure) {
                    primary = failure;
                    throw;
                } finally {
                    try {
                        await owned.DisposeAsync();
                    } catch (Exception cleanup) when (primary is not null) {
                        throw new AggregateException("The lifecycle browser connection and reference release failed.", primary, cleanup);
                    }
                }
            }
        } finally {
            initialization.Release();
        }
    }

    private sealed record WireInitialization(int Schema);
    private sealed record WireKey(string DatabaseProfileId, string Namespace, string CallerBinding, string Epoch, string Id);
    private sealed record WireRecord(int Schema, WireKey Key, long Revision, string Stage, string FrozenRequest,
        int Kind, Guid[] TargetProjectIds, Guid? AttemptId, bool HadUnconfirmedDispatch, string? AcceptedPayload, string? AbsenceProof);
    private sealed record WireChange(string Status, WireRecord? Record);
    private sealed record WireMetadata(WireKey Key, long Revision, string Stage, int Kind, Guid[] TargetProjectIds, bool HadUnconfirmedDispatch);
    private sealed record WirePage(WireMetadata[] Items, WireKey? NextKey);
}

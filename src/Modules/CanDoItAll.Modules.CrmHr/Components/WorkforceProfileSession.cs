using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record WorkforceProfileRead(WorkforceProfileWorkspaceModel? Workspace, long Opening, long HintRevision);
public enum WorkforceWriteArea { Profile, Records, Catalog }

public sealed class WorkforceProfileSession(IWorkforceProfileEditing hr, CrmHrChanges changes, EditingChangeFeed feed,
    IEditingUiAccess access, ILogger<EditingObservation> observationLog, ILogger<WorkforceProfileSession> logger) : IAsyncDisposable {
    private EditingObservation? profileObservation;
    private EditingObservation? recordsObservation;
    private EditingObservation? catalogObservation;
    private Task retirement = Task.CompletedTask;
    private CancellationTokenSource? reviewRead;
    private readonly Queue<Guid> operations = new();
    private readonly Dictionary<Guid, string> names = [];
    private Guid? target;
    private Guid editorId;
    private long generation;
    private long hintRevision;
    private bool disposed;
    public WorkforceProfileReconciliation State { get; } = new();
    public WorkforceProfileEditorModel? CurrentProfile { get; private set; }
    public string? Message { get; private set; }
    public bool RequiresAttention { get; private set; }
    public bool Reading { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action RecordsChanged { get; init; }
    public required Action CatalogChanged { get; init; }
    public Action ContextChanged { get; init; } = () => { };
    public required Action Notice { get; init; }
    public string Actor => profileObservation?.Actor ?? throw new InvalidOperationException("Open the workforce record with current access first.");

    public string PartyName(Guid? id) => id is { } key ? names.GetValueOrDefault(key) ?? "Referenced record" : "None";

    public async Task AuthorizeCatalogReadAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var grant = await access.OpenAsync(new(changes.Scope, EditingOwner.CrmWorkforceProfile), cancellationToken);
        await access.ValidateAsync(grant, write: false, cancellationToken);
    }

    public async Task<WorkforceProfileRead> ReadWorkspaceAsync(Guid partyId, CancellationToken cancellationToken) {
        await OpenAsync(partyId, cancellationToken);
        var current = profileObservation!;
        var records = recordsObservation!;
        var catalog = catalogObservation!;
        var opening = generation;
        var revision = hintRevision;
        using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, current.Lifetime, records.Lifetime, catalog.Lifetime);
        await AuthorizeReadsAsync([current, records, catalog], read.Token);
        var workspace = await hr.GetWorkforceProfileWorkspaceAsync(partyId, read.Token);
        await AuthorizeReadsAsync([current, records, catalog], read.Token);
        if (workspace is not null && (workspace.PartyId != partyId || workspace.Profile.PartyId != partyId || workspace.Profile.Original?.PartyId != partyId)) {
            throw new InvalidOperationException("The workforce read returned a profile from another record or omitted its original.");
        }
        return new(workspace, opening, revision);
    }

    public void Observe(WorkforceProfileRead read, bool refreshDraft = false) {
        if (disposed || read.Opening != generation || read.Workspace is not { } workspace || target != workspace.PartyId) {
            return;
        }
        CurrentProfile = workspace.Profile.Snapshot();
        if (State.Draft.PartyId != workspace.PartyId) {
            State.Open(workspace.Profile);
        } else if (!refreshDraft && workspace.Profile.Original != State.Draft.Original) {
            State.Hint();
        } else {
            State.Observe(workspace.Profile);
        }
        if (workspace.Profile.HomeUnitPartyId is { } home) {
            names[home] = workspace.HomeUnitName;
        }
        if (workspace.Profile.ManagerPartyId is { } manager) {
            names[manager] = workspace.ManagerName;
        }
        PruneNames();
        if (read.HintRevision != hintRevision) {
            State.Hint();
        }
        if (refreshDraft && !State.IsOutdated && !State.IsUnknown && State.Review is null) {
            Message = null;
            RequiresAttention = false;
        }
    }

    private async Task OpenAsync(Guid partyId, CancellationToken cancellationToken) {
        if (target == partyId && profileObservation is { } existing) {
            await existing.AuthorizeAsync(write: false, cancellationToken);
            return;
        }
        Clear();
        var opening = generation;
        await retirement;
        if (disposed || opening != generation) {
            throw new OperationCanceledException("The workforce opening was superseded.");
        }
        target = partyId;
        editorId = Guid.NewGuid();
        profileObservation = new(feed, access, new(changes.Scope, EditingOwner.CrmWorkforceProfile, partyId), observationLog);
        recordsObservation = new(feed, access, new(changes.Scope, EditingOwner.CrmWorkforceRecords, partyId), observationLog);
        catalogObservation = new(feed, access, new(changes.Scope, EditingOwner.CrmSkillCatalog), observationLog);
        try {
            await StartAsync(profileObservation, opening, batch => {
                hintRevision++;
                ContextChanged();
                if (State.Draft.PartyId == partyId) {
                    var alreadyOutdated = State.IsOutdated;
                    State.Hint();
                    if (!State.IsUnknown) {
                        Message = "Another editor changed the workforce profile. Refresh or review the saved changes before saving your draft.";
                        RequiresAttention = true;
                    }
                    if (!alreadyOutdated && !State.IsUnknown) {
                        Notice();
                    }
                }
                return Task.CompletedTask;
            });
            await StartAsync(recordsObservation, opening, _ => {
                RecordsChanged();
                return Task.CompletedTask;
            });
            await StartAsync(catalogObservation, opening, _ => {
                CatalogChanged();
                return Task.CompletedTask;
            }, catalog: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed || generation != opening) {
                throw new OperationCanceledException("The workforce opening was superseded.");
            }
        } catch {
            if (opening == generation) {
                Clear();
            }
            throw;
        }
    }

    private Task StartAsync(EditingObservation observation, long opening, Func<EditingChangeBatch, Task> received, bool catalog = false) =>
        observation.StartAsync(batch => Dispatch(async () => {
            if (disposed || opening != generation || !batch.RequiresResync && batch.Changes.All(IsOwn)) {
                return;
            }
            await received(batch);
            Changed();
        }), () => Dispatch(() => {
            if (!disposed && opening == generation) {
                Clear();
                Retired();
                Changed();
            }
            return Task.CompletedTask;
        }), catalog);

    public bool IsOwn(EditingChange change) => change.OriginEditorId == editorId && operations.Contains(change.OperationId);

    private static async Task AuthorizeReadsAsync(IReadOnlyList<EditingObservation> observations, CancellationToken cancellationToken) {
        foreach (var observation in observations) {
            await observation.AuthorizeAsync(write: false, cancellationToken);
        }
    }

    public Func<CancellationToken, ValueTask> CaptureReadAdmission(Guid partyId, WorkforceWriteArea area) => CaptureAdmission(partyId, area, write: false);

    public Func<CancellationToken, ValueTask> CaptureWriteAdmission(Guid partyId, WorkforceWriteArea area) => CaptureAdmission(partyId, area, write: true);

    private Func<CancellationToken, ValueTask> CaptureAdmission(Guid partyId, WorkforceWriteArea area, bool write) {
        var opening = generation;
        var current = area switch {
            WorkforceWriteArea.Profile => profileObservation,
            WorkforceWriteArea.Records => recordsObservation,
            WorkforceWriteArea.Catalog => catalogObservation,
            _ => throw new ArgumentOutOfRangeException(nameof(area))
        };
        if (current is null || disposed || target != partyId) {
            throw new UnauthorizedAccessException("Open the workforce record with current access before writing.");
        }
        return token => {
            if (disposed || opening != generation || target != partyId) {
                throw new UnauthorizedAccessException("The workforce write no longer belongs to its authorized opening.");
            }
            return current.AuthorizeAsync(write, token);
        };
    }

    public IDisposable EnterWrite(Guid partyId) {
        if (disposed || target != partyId || profileObservation is null) {
            throw new UnauthorizedAccessException("The workforce write no longer belongs to its opening.");
        }
        var operation = Guid.NewGuid();
        operations.Enqueue(operation);
        while (operations.Count > 32) {
            operations.Dequeue();
        }
        return changes.Enter(new(editorId, operation));
    }

    public async Task<bool> SaveAsync(WorkforceProfileEditorModel model) {
        if (!ReferenceEquals(model, State.Draft) || !State.CanSave || Reading || profileObservation is not { } current) {
            return false;
        }
        var opening = generation;
        var partyId = model.PartyId;
        model.LastChangedBy = current.Actor;
        var submission = State.Begin()!;
        Message = null;
        RequiresAttention = false;
        var ownerStarted = false;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (opening != generation || !State.IsCurrent(submission)) {
                return false;
            }
            using var authority = changes.EnterUiWrite(token => {
                if (disposed || opening != generation || !State.IsCurrent(submission)) {
                    throw new UnauthorizedAccessException("The workforce opening retired before commit.");
                }
                return current.AuthorizeAsync(write: true, token);
            });
            using var origin = EnterWrite(partyId);
            ownerStarted = true;
            var result = await hr.SaveWorkforceProfileAsync(submission.Model, onCommitted: receipt => {
                if (State.IsCurrent(submission)) {
                    State.Accept(submission, receipt);
                    CurrentProfile = receipt.Accepted.Snapshot();
                }
            });
            if (opening != generation || !State.IsCurrent(submission)) {
                return false;
            }
            if (result.IsFailure) {
                if (result.Errors.Any(error => error.Code == CrmHrEditingErrorCodes.WorkforceConflict)) {
                    State.Hint();
                }
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                Notice();
                return false;
            }
            if (State.Accepted is null) {
                throw new InvalidOperationException("The workforce owner returned success without its committed receipt.");
            }
            Message = State.IsDirty ? "Workforce profile saved. Later input is retained." : "Workforce profile saved.";
            return true;
        } catch (UnauthorizedAccessException exception) {
            logger.LogWarning(exception, "Workforce authority retired for {PartyId}; accepted {Accepted}.", partyId, State.Accepted is not null);
            if (opening == generation && State.IsCurrent(submission)) {
                RequiresAttention = true;
                Message = State.Accepted is null ? "Current workforce write access is required. Your draft is retained."
                    : "The workforce profile committed. Follow-up access failed; the accepted write will not be repeated.";
                Notice();
                return State.Accepted is not null;
            }
            return false;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Workforce profile continuation failed for {PartyId}; accepted {Accepted}.", partyId, State.Accepted is not null);
            if (opening == generation && State.IsCurrent(submission)) {
                RequiresAttention = true;
                if (ownerStarted && State.Accepted is null) {
                    State.Unknown(submission);
                    Message = "The workforce save outcome is unknown. Its submission and your draft are retained; resubmission is blocked.";
                } else {
                    Message = State.Accepted is not null ? "The workforce profile committed. Follow-up failed; the accepted write will not be repeated."
                        : "Current workforce access is required. Your draft is retained.";
                }
                Notice();
                return State.Accepted is not null;
            }
            return false;
        } finally {
            State.Complete(submission);
            Changed();
        }
    }

    public Task ReviewAsync() => ReadCurrentAsync();

    public void RefreshFailed() {
        if (State.Accepted is not null && !State.IsUnknown) {
            Message = "The workforce profile committed, but current context could not be refreshed. The accepted write will not be repeated.";
            RequiresAttention = true;
            Changed();
        }
    }

    public async Task ApplyReviewAsync() {
        if (!State.CanApply || Reading || profileObservation is not { } current) {
            return;
        }
        var opening = generation;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (opening == generation && State.Apply()) {
                Message = "Changes applied to your draft. Save remains a separate action.";
                RequiresAttention = false;
            }
        } catch (UnauthorizedAccessException) {
            if (opening == generation) {
                Message = "Current workforce write access is required. Your draft and review are retained.";
                RequiresAttention = true;
                Notice();
            }
        }
        Changed();
    }

    public async Task UndoAsync() {
        if (!State.CanUndo || profileObservation is not { } current) {
            return;
        }
        var opening = generation;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (opening == generation) {
                State.Undo();
            }
        } catch (UnauthorizedAccessException) {
            if (opening == generation) {
                Message = "Current workforce write access is required. Your draft is retained.";
                RequiresAttention = true;
                Notice();
            }
        }
        Changed();
    }

    private async Task ReadCurrentAsync() {
        if (profileObservation is not { } current || target is not { } partyId || State.Draft.PartyId != partyId) {
            return;
        }
        var opening = generation;
        var revision = hintRevision;
        var stateHint = State.BeginReview();
        using var read = CancellationTokenSource.CreateLinkedTokenSource(current.Lifetime);
        Interlocked.Exchange(ref reviewRead, read)?.Cancel();
        Reading = true;
        Changed();
        try {
            await current.AuthorizeAsync(write: false, read.Token);
            var found = await hr.ReadWorkforceProfileEditorAsync(partyId, read.Token);
            await current.AuthorizeAsync(write: false, read.Token);
            if (disposed || opening != generation || !ReferenceEquals(reviewRead, read)) {
                return;
            }
            if (found is not { } value) {
                State.Unavailable();
                if (!State.IsUnknown) {
                    Message = "The workforce profile is no longer available. Your draft is retained.";
                }
                RequiresAttention = true;
                return;
            }
            foreach (var name in value.Names) {
                names[name.Key] = name.Value;
            }
            CurrentProfile = value.Profile.Snapshot();
            State.Compare(value.Profile, stateHint);
            if (revision != hintRevision) {
                State.Hint();
            }
            PruneNames();
        } catch (OperationCanceledException) when (read.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (!disposed && opening == generation) {
                Clear();
                Retired();
                Changed();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Workforce profile current read failed for {PartyId}.", partyId);
            if (!disposed && opening == generation && ReferenceEquals(reviewRead, read)) {
                State.Hint();
                if (!State.IsUnknown) {
                    Message = "The current workforce profile could not be read. Your draft and review choices are retained.";
                }
                RequiresAttention = true;
                Notice();
            }
        } finally {
            if (ReferenceEquals(Interlocked.CompareExchange(ref reviewRead, null, read), read)) {
                Reading = false;
                Changed();
            }
        }
    }

    private void PruneNames() {
        var keep = new Guid?[] { State.Draft.HomeUnitPartyId, State.Draft.ManagerPartyId,
            CurrentProfile?.HomeUnitPartyId, CurrentProfile?.ManagerPartyId,
            State.Review?.HomeUnit.Original, State.Review?.HomeUnit.Local, State.Review?.HomeUnit.Current,
            State.Review?.Manager.Original, State.Review?.Manager.Local, State.Review?.Manager.Current }.OfType<Guid>().ToHashSet();
        foreach (var id in names.Keys.Where(id => !keep.Contains(id)).ToArray()) {
            names.Remove(id);
        }
    }

    public void Clear() {
        generation++;
        target = null;
        reviewRead?.Cancel();
        reviewRead = null;
        Reading = false;
        Message = null;
        CurrentProfile = null;
        RequiresAttention = false;
        State.Clear();
        names.Clear();
        operations.Clear();
        hintRevision = 0;
        var previous = new[] { profileObservation, recordsObservation, catalogObservation }.OfType<EditingObservation>().ToArray();
        profileObservation = recordsObservation = catalogObservation = null;
        retirement = RetireAsync(retirement, previous);
    }

    private static async Task RetireAsync(Task earlier, IReadOnlyList<EditingObservation> observations) {
        List<Exception>? failures = null;
        try {
            await earlier;
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }
        foreach (var observation in observations) {
            try {
                await observation.DisposeAsync();
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            }
        }
        if (failures is not null) {
            throw new AggregateException("Workforce observation retirement failed.", failures);
        }
    }

    public Task CloseAsync() {
        Clear();
        return retirement;
    }

    public async ValueTask DisposeAsync() {
        if (!disposed) {
            disposed = true;
            await CloseAsync();
        }
    }
}

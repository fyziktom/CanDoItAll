using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public enum RecruitmentWriteArea { Application, Interviews, Tasks, Support, Context }

public sealed class RecruitmentApplicationSession(IRecruitmentApplicationEditing owner, CrmHrChanges changes,
    EditingChangeFeed feed, IEditingUiAccess access, ILogger<EditingObservation> observationLog,
    ILogger<RecruitmentApplicationSession> logger) : IAsyncDisposable {
    private readonly Dictionary<RecruitmentWriteArea, EditingObservation> observations = [];
    private readonly Queue<Guid> operations = new();
    private readonly Dictionary<Guid, string> names = [];
    private Task retirement = Task.CompletedTask;
    private CancellationTokenSource? currentRead;
    private Guid editorId;
    private long generation;
    private long binding;
    private bool disposed;
    public RecruitmentApplicationState State { get; } = new();
    public RecruitmentApplicationEditorModel? CurrentApplication { get; private set; }
    public string? Message { get; private set; }
    public bool RequiresAttention { get; private set; }
    public bool Reading { get; private set; }
    public long Opening => generation;
    public long Epoch { get; private set; }
    public bool CanSave => observations.ContainsKey(RecruitmentWriteArea.Application) && State.CanSave && !Reading;
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }
    public required Action<RecruitmentWriteArea> ContextChanged { get; init; }
    public string PartyName(Guid? id) => id is { } key ? names.GetValueOrDefault(key) ?? "Referenced party" : "None";
    public string Actor => Get(RecruitmentWriteArea.Application).Actor;

    public async Task<T> ReadWithAccessAsync<T>(Guid? resourceId, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var grant = await access.OpenAsync(new(changes.Scope, EditingOwner.CrmRecruitmentApplication, resourceId), cancellationToken);
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(grant.Lifetime, cancellationToken);
        await access.ValidateAsync(grant, write: false, reading.Token);
        var result = await read(reading.Token);
        await access.ValidateAsync(grant, write: false, reading.Token);
        ObjectDisposedException.ThrowIf(disposed, this);
        return result;
    }

    public async Task<bool> OpenAsync(RecruitmentApplicationEditorModel model) {
        Clear();
        var opening = generation;
        await retirement;
        if (!IsCurrent(opening)) {
            return false;
        }
        State.Open(model);
        CurrentApplication = model.Snapshot();
        editorId = Guid.NewGuid();
        try {
            await BindAsync(opening);
            if (model.Id.HasValue) {
                await ReadCurrentAsync(review: false, explicitPull: false);
            }
        } catch (UnauthorizedAccessException) {
            Deny();
            return false;
        }
        Changed();
        return IsCurrent(opening);
    }

    private async Task BindAsync(long opening) {
        var currentBinding = ++binding;
        await StartAsync(RecruitmentWriteArea.Application, EditingOwner.CrmRecruitmentApplication, State.Draft.Id);
        if (State.Draft.Id is { } applicationId && State.Draft.PartyId is { } partyId) {
            await StartAsync(RecruitmentWriteArea.Interviews, EditingOwner.CrmRecruitmentInterviews, applicationId);
            await StartAsync(RecruitmentWriteArea.Tasks, EditingOwner.CrmRecruitmentTasks, partyId);
            await StartAsync(RecruitmentWriteArea.Support, EditingOwner.CrmHrRelationships, partyId);
            await StartAsync(RecruitmentWriteArea.Context, EditingOwner.CrmWorkforceProfile, partyId);
        }
        async Task StartAsync(RecruitmentWriteArea area, EditingOwner resourceOwner, Guid? id) {
            var source = new EditingObservation(feed, access, new(changes.Scope, resourceOwner, id), observationLog);
            observations.Add(area, source);
            await source.StartAsync(batch => Dispatch(() => {
                if (!IsCurrent(opening) || currentBinding != binding || !batch.RequiresResync && batch.Changes.All(IsOwn)) {
                    return Task.CompletedTask;
                }
                if (area == RecruitmentWriteArea.Application) {
                    var alreadyOutdated = State.IsOutdated;
                    State.Hint();
                    Message = "Another editor changed this application. Refresh or review the saved changes before replacing it.";
                    RequiresAttention = true;
                    if (!alreadyOutdated) {
                        Notice();
                    }
                }
                ContextChanged(area);
                Changed();
                return Task.CompletedTask;
            }), () => Dispatch(() => {
                if (IsCurrent(opening) && currentBinding == binding) {
                    Deny();
                }
                return Task.CompletedTask;
            }));
        }
    }

    public bool IsOwn(EditingChange change) => change.OriginEditorId == editorId && operations.Contains(change.OperationId);
    private bool IsCurrent(long opening) => !disposed && opening == generation;
    private EditingObservation Get(RecruitmentWriteArea area) => observations.TryGetValue(area, out var source) && !disposed
        ? source : throw new UnauthorizedAccessException("Open the recruitment record with current access first.");

    public Func<CancellationToken, ValueTask> CaptureAdmission(RecruitmentWriteArea area, bool write) {
        var source = Get(area);
        var opening = generation;
        var currentBinding = binding;
        return async token => {
            if (!IsCurrent(opening) || currentBinding != binding) {
                throw new UnauthorizedAccessException("The recruitment command no longer belongs to its authorized opening.");
            }
            await source.AuthorizeAsync(write, token);
            if (!IsCurrent(opening) || currentBinding != binding) {
                throw new UnauthorizedAccessException("The recruitment opening retired while its access was being checked.");
            }
        };
    }
    public IDisposable EnterWrite() {
        Get(RecruitmentWriteArea.Application);
        var operation = Guid.NewGuid();
        operations.Enqueue(operation);
        while (operations.Count > 32) {
            operations.Dequeue();
        }
        return changes.Enter(new(editorId, operation));
    }

    public async Task<bool> SaveAsync(RecruitmentApplicationEditorModel model) {
        if (!ReferenceEquals(model, State.Draft) || !CanSave) {
            return false;
        }
        var opening = generation;
        var admission = CaptureAdmission(RecruitmentWriteArea.Application, write: true);
        model.LastChangedBy = Actor;
        var submission = State.Begin()!;
        Epoch++;
        var ownerStarted = false;
        Message = null;
        RequiresAttention = false;
        try {
            await admission(default);
            if (!IsCurrent(opening) || !State.IsCurrent(submission)) {
                return false;
            }
            using var authority = changes.EnterUiWrite(async token => {
                if (!IsCurrent(opening) || !State.IsCurrent(submission)) {
                    throw new UnauthorizedAccessException("The recruitment opening retired before commit.");
                }
                await admission(token);
                if (!IsCurrent(opening) || !State.IsCurrent(submission)) {
                    throw new UnauthorizedAccessException("The recruitment submission retired while its access was being checked.");
                }
            });
            using var origin = EnterWrite();
            ownerStarted = true;
            var result = await owner.SaveRecruitmentApplicationAsync(submission.Model, onCommitted: receipt => {
                if (State.IsCurrent(submission)) {
                    State.Accept(submission, receipt);
                    CurrentApplication = receipt.Accepted.Snapshot();
                }
            });
            if (!IsCurrent(opening) || !State.IsCurrent(submission)) {
                return false;
            }
            if (result.IsFailure) {
                if (result.Errors.Any(error => error.Code == RecruitmentEditingErrors.Conflict)) {
                    State.Hint();
                }
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                Notice();
                return false;
            }
            if (State.Accepted is null) {
                throw new InvalidOperationException("The recruitment owner returned success without its committed receipt.");
            }
            Message = State.IsDirty ? "Application saved. Later input is retained." : "Application saved.";
            if (submission.Model.Id is null) {
                await RetargetCreatedAsync(opening);
            }
            return true;
        } catch (UnauthorizedAccessException exception) {
            logger.LogWarning(exception, "Recruitment write authority retired for {ApplicationId}; accepted {Accepted}.", submission.Model.Id, State.Accepted is not null);
            if (IsCurrent(opening) && State.IsCurrent(submission)) {
                Message = State.Accepted is null ? "Current recruitment write access is required. Your draft is retained."
                    : "The application committed. Follow-up access failed; the accepted write will not be repeated.";
                RequiresAttention = true;
                Notice();
            }
            return State.Accepted is not null;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Recruitment continuation failed for {ApplicationId}; accepted {Accepted}.", submission.Model.Id, State.Accepted is not null);
            if (IsCurrent(opening) && State.IsCurrent(submission)) {
                if (ownerStarted && State.Accepted is null) {
                    State.Unknown(submission);
                }
                Message = State.IsUnknown ? "The application save outcome is unknown. The submission and your draft are retained; resubmission is blocked."
                    : State.Accepted is not null ? "The application committed. Follow-up failed; the accepted write will not be repeated."
                    : "The application was not submitted. Your draft is retained.";
                RequiresAttention = true;
                Notice();
            }
            return State.Accepted is not null;
        } finally {
            State.Complete(submission);
            Epoch++;
            Changed();
        }
    }

    private async Task RetargetCreatedAsync(long opening) {
        RetireObservations();
        await retirement;
        if (IsCurrent(opening)) {
            await BindAsync(opening);
            await ReadCurrentAsync(review: false, explicitPull: false);
        }
    }

    public Task ReviewAsync() => ReadCurrentAsync(review: true, explicitPull: false);
    public Task RefreshApplicationAsync() => ReadCurrentAsync(review: false, explicitPull: true);
    public void AcceptConversion(RecordSubmission<RecruitmentApplicationEditorModel, RecruitmentApplicationField> submission,
        RecruitmentConversionReceipt receipt) {
        if (State.IsCurrent(submission)) {
            State.Accept(submission, new(receipt.ApplicationId, receipt.Application));
            CurrentApplication = receipt.Application.Snapshot();
        }
    }
    public void Observe(RecruitmentApplicationEditorModel current, long readHint, bool explicitPull) {
        if (current.Id != State.Draft.Id) {
            throw new InvalidOperationException("The recruitment read belongs to another opening.");
        }
        if (RejectOlderRead(current)) {
            return;
        }
        CurrentApplication = current.Snapshot();
        State.Observe(current, readHint, explicitPull);
        if (!State.IsOutdated && !State.IsUnknown && State.Review is null && explicitPull) {
            Message = null;
            RequiresAttention = false;
        }
    }
    public Task ApplyAsync() => ChangeDraftAsync(() => {
        if (State.Apply()) {
            Message = "Changes applied to your draft. Save remains a separate action.";
            RequiresAttention = false;
        }
    });
    public Task UndoAsync() => ChangeDraftAsync(State.Undo);
    private async Task ChangeDraftAsync(Action change) {
        var opening = generation;
        try {
            await CaptureAdmission(RecruitmentWriteArea.Application, write: true)(default);
            if (IsCurrent(opening)) {
                change();
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Message = "Current application write access is required. Your draft and comparison are retained.";
                RequiresAttention = true;
                Notice();
            }
        } finally {
            Changed();
        }
    }

    private async Task ReadCurrentAsync(bool review, bool explicitPull) {
        if (State.Draft.Id is not { } id || Reading || State.IsSaving && review) {
            return;
        }
        var source = Get(RecruitmentWriteArea.Application);
        var opening = generation;
        var readHint = State.HintRevision;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(source.Lifetime);
        Interlocked.Exchange(ref currentRead, reading)?.Cancel();
        Reading = true;
        Changed();
        try {
            await source.AuthorizeAsync(write: false, reading.Token);
            var read = await owner.ReadApplicationAsync(id, State.ReferenceIds, reading.Token);
            await source.AuthorizeAsync(write: false, reading.Token);
            if (!IsCurrent(opening) || !ReferenceEquals(currentRead, reading)) {
                return;
            }
            if (read is null) {
                State.Unavailable();
                Message = "The application is no longer available. Your draft is retained.";
                RequiresAttention = true;
                return;
            }
            if (read.Model.Id != id) {
                throw new InvalidOperationException("The current recruitment read belongs to another application.");
            }
            if (RejectOlderRead(read.Model)) {
                return;
            }
            CurrentApplication = read.Model.Snapshot();
            if (review) {
                State.Compare(read.Model, readHint);
            } else {
                Observe(read.Model, readHint, explicitPull);
            }
            foreach (var pair in read.Names) {
                names[pair.Key] = pair.Value;
            }
            var keep = State.ReferenceIds.Append(State.Draft.PartyId ?? Guid.Empty).ToHashSet();
            foreach (var key in names.Keys.Where(key => !keep.Contains(key)).ToArray()) {
                names.Remove(key);
            }
        } catch (OperationCanceledException) when (reading.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Deny();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Recruitment current read failed for {ApplicationId}.", id);
            if (IsCurrent(opening)) {
                State.Hint();
                Message = "The current application could not be read. Your draft and review choices are retained.";
                RequiresAttention = true;
                Notice();
            }
        } finally {
            if (ReferenceEquals(Interlocked.CompareExchange(ref currentRead, null, reading), reading)) {
                Reading = false;
                Changed();
            }
        }
    }

    public void RefreshFailed() {
        if (State.Accepted is not null) {
            Message = "The application committed, but its context could not be refreshed. The accepted write will not be repeated.";
            RequiresAttention = true;
            Changed();
        }
    }
    private bool RejectOlderRead(RecruitmentApplicationEditorModel current) {
        if (!State.IsOlderThanBasis(current)) {
            return false;
        }
        State.Hint();
        Message = "An older read was ignored. The newer accepted application and your draft are retained; refresh the current saved values.";
        RequiresAttention = true;
        return true;
    }
    private void Deny() {
        Clear();
        Retired();
        Changed();
    }
    public void Clear() {
        generation++;
        Epoch++;
        currentRead?.Cancel();
        currentRead = null;
        Reading = false;
        Message = null;
        RequiresAttention = false;
        CurrentApplication = null;
        State.Open(new());
        names.Clear();
        operations.Clear();
        RetireObservations();
    }
    private void RetireObservations() {
        binding++;
        var previous = observations.Values.ToArray();
        observations.Clear();
        retirement = RetireAsync(retirement, previous);
    }
    private static async Task RetireAsync(Task earlier, IReadOnlyList<EditingObservation> previous) {
        List<Exception>? failures = null;
        try {
            await earlier;
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }
        foreach (var source in previous) {
            try {
                await source.DisposeAsync();
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            }
        }
        if (failures is not null) {
            throw new AggregateException("Recruitment observation retirement failed.", failures);
        }
    }
    public Task CloseAsync() {
        Clear();
        return retirement;
    }
    public async ValueTask DisposeAsync() {
        if (!disposed) {
            Clear();
            disposed = true;
            await retirement;
        }
    }
}

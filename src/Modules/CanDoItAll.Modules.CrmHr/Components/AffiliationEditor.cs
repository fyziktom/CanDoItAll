using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record AffiliationRead(Guid PersonId, IReadOnlyList<PartyOrganizationAffiliationListItemModel> Rows, long Opening, long HintRevision);
public sealed record AffiliationSubmission(Guid PersonId, IReadOnlyList<PartyOrganizationAffiliationEditorModel> Rows, long Opening);

public sealed class AffiliationEditor(IPartyOrganizationAffiliationService affiliations, CrmHrChanges changes, EditingChangeFeed feed,
    IEditingUiAccess access, IClock clock, ILogger<EditingObservation> observationLog, ILogger<AffiliationEditor> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private Guid? personId;
    private Guid editorId = Guid.NewGuid();
    private readonly Queue<Guid> operations = new();
    private long generation;
    private long hintRevision;
    private long reviewGeneration;
    private CancellationTokenSource? reviewRead;
    private bool initialized;
    private bool disposed;
    public AffiliationReconciliation State { get; } = new();
    public IReadOnlyList<PartyOrganizationAffiliationListItemModel> Saved { get; private set; } = [];
    public long RenderRevision { get; private set; }
    public bool ReadingReview { get; private set; }
    public string? Message { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }

    public async Task<AffiliationRead> ReadAsync(Guid id, CancellationToken cancellationToken = default) {
        if (observation is null || personId != id) {
            RetireObservation();
            var opening = generation;
            await retirement;
            if (disposed || opening != generation) {
                throw new OperationCanceledException("The affiliation opening was superseded.");
            }
            personId = id;
            initialized = false;
            editorId = Guid.NewGuid();
            operations.Clear();
            var current = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmHrAffiliations, id), observationLog);
            observation = current;
            await current.StartAsync(batch => Dispatch(() => ReceiveAsync(batch, opening)), () => Dispatch(() => {
                if (!disposed && opening == generation) {
                    Deny();
                }
                return Task.CompletedTask;
            }));
        }
        var source = observation;
        var request = generation;
        var revision = hintRevision;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, source!.Lifetime);
        await source.AuthorizeAsync(write: false, reading.Token);
        var rows = await affiliations.ListAsync(id, reading.Token);
        await source.AuthorizeAsync(write: false, reading.Token);
        return new(id, rows, request, revision);
    }

    public void Observe(AffiliationRead read) {
        if (!IsCurrent(read.Opening, read.PersonId)) {
            return;
        }
        if (!initialized) {
            State.Reset(read.Rows);
            initialized = true;
        } else {
            State.Observe(read.Rows);
        }
        Saved = read.Rows;
        RenderRevision++;
        if (hintRevision != read.HintRevision) {
            State.Hint();
        }
    }

    public AffiliationSubmission PrepareSave() {
        if (ReadingReview || !initialized || personId is not Guid id || observation is null) {
            throw new InvalidOperationException("Open the affiliation owner before saving.");
        }
        return new(id, State.BeginSave(), generation);
    }

    public void CancelSubmission(AffiliationSubmission submitted) {
        if (IsCurrent(submitted.Opening, submitted.PersonId)) {
            State.Rejected(conflict: false);
        }
    }

    public async Task<Result> CommitAsync(AffiliationSubmission submitted) {
        if (!IsCurrent(submitted.Opening, submitted.PersonId)) {
            return Conflict();
        }
        if (observation is not { } current || !State.IsSaving || State.IsOutdated) {
            State.Rejected(conflict: true);
            return Conflict();
        }
        IReadOnlyList<PartyOrganizationAffiliationListItemModel>? accepted = null;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (!IsCurrent(submitted.Opening, submitted.PersonId)) {
                return Conflict();
            }
            if (State.IsOutdated || State.IsUnknown) {
                State.Rejected(conflict: true);
                return Conflict();
            }
            var operation = Guid.NewGuid();
            operations.Enqueue(operation);
            while (operations.Count > 32) {
                operations.Dequeue();
            }
            using var origin = changes.Enter(new(editorId, operation));
            using var admission = changes.EnterUiWrite(token => current.AuthorizeAsync(write: true, token));
            var result = await affiliations.ReplaceAsync(submitted.PersonId, submitted.Rows.ToArray(), current.Actor,
                onCommitted: rows => accepted = rows);
            if (IsCurrent(submitted.Opening, submitted.PersonId)) {
                if (result.IsSuccess) {
                    Accept(submitted, result.Value!);
                    Message = null;
                } else {
                    State.Rejected(result.Errors.Any(error => error.Code is CrmHrEditingErrorCodes.AffiliationConflict or CrmHrEditingErrorCodes.AffiliationPersistenceConflict));
                    Message = string.Join(" ", result.Errors.Select(error => error.Message));
                }
            }
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Errors);
        } catch (UnauthorizedAccessException) when (accepted is null) {
            if (IsCurrent(submitted.Opening, submitted.PersonId)) {
                Deny();
            }
            return Result.Failure(Error.Failure("Current affiliation editing access is required.", CrmHrEditingErrorCodes.EditingAccessDenied));
        } catch (Exception exception) {
            logger.LogWarning(exception, "Affiliation continuation failed for person {PersonId}; accepted {Accepted}.", submitted.PersonId, accepted is not null);
            if (IsCurrent(submitted.Opening, submitted.PersonId)) {
                if (accepted is not null) {
                    Accept(submitted, accepted);
                    Message = "Affiliations were saved. A follow-up failed; the accepted write will not be repeated.";
                } else {
                    State.Unknown(submitted.Rows);
                    Message = "The affiliation save outcome is unknown. Its submission and your draft are retained; resubmission is blocked.";
                }
            }
            return accepted is not null ? Result.Success() : Result.Failure(Error.Failure("The affiliation save outcome is unknown.", CrmHrEditingErrorCodes.AffiliationOutcomeUnknown));
        } finally {
            if (IsCurrent(submitted.Opening, submitted.PersonId)) {
                Changed();
            }
        }
    }

    private void Accept(AffiliationSubmission submitted, IReadOnlyList<PartyOrganizationAffiliationListItemModel> accepted) {
        State.Accepted(submitted.Rows, accepted);
        Saved = accepted;
        RenderRevision++;
    }

    public async Task ReviewAsync() {
        if (ReadingReview || State.IsSaving || observation is not { } current || personId is not Guid id) {
            return;
        }
        var opening = generation;
        var revision = State.BeginReview();
        var attempt = ++reviewGeneration;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(current.Lifetime);
        reviewRead = reading;
        ReadingReview = true;
        try {
            await current.AuthorizeAsync(write: false, reading.Token);
            var saved = await affiliations.ListAsync(id, reading.Token);
            await current.AuthorizeAsync(write: false, reading.Token);
            if (IsCurrent(opening, id) && attempt == reviewGeneration) {
                State.Compare(saved, revision, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
                Message = null;
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening, id)) {
                Deny();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening, id) || attempt != reviewGeneration) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Affiliation comparison failed for person {PersonId}.", id);
            if (IsCurrent(opening, id) && attempt == reviewGeneration) {
                Message = "Saved affiliations could not be read. Your draft and comparison choices are retained.";
            }
        } finally {
            if (IsCurrent(opening, id) && attempt == reviewGeneration) {
                ReadingReview = false;
                reviewRead = null;
                Changed();
            }
        }
    }

    public bool ApplyReview() {
        if (ReadingReview || !State.Apply()) {
            return false;
        }
        RenderRevision++;
        Changed();
        return true;
    }

    public void Undo() {
        State.Undo();
        RenderRevision++;
        Changed();
    }

    public void CancelReview() {
        CancelReviewRead();
        State.CancelReview();
        Changed();
    }

    private async Task ReceiveAsync(EditingChangeBatch batch, long opening) {
        if (!IsCurrent(opening, personId) || observation is not { } current ||
            !batch.RequiresResync && batch.Changes.All(change => change.OriginEditorId == editorId && operations.Contains(change.OperationId))) {
            return;
        }
        hintRevision++;
        if (!initialized) {
            return;
        }
        State.Hint();
        if (!State.IsDirty && !State.IsSaving && !State.IsUnknown && State.Review is null) {
            try {
                await current.AuthorizeAsync(write: false, current.Lifetime);
                var saved = await affiliations.ListAsync(personId!.Value, current.Lifetime);
                await current.AuthorizeAsync(write: false, current.Lifetime);
                if (!IsCurrent(opening, personId)) {
                    return;
                }
                State.Observe(saved);
                Saved = saved;
                RenderRevision++;
            } catch (UnauthorizedAccessException) {
                throw;
            } catch (OperationCanceledException) when (disposed || opening != generation) {
                return;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Affiliation observation failed for person {PersonId}.", personId);
                if (disposed || opening != generation) {
                    return;
                }
                Message = "Saved affiliations could not be refreshed. Your draft is retained; retry the comparison.";
            }
        }
        if (State.IsOutdated) {
            Notice();
        }
        Changed();
    }

    public void Clear() {
        RetireObservation();
        State.Reset([]);
        Saved = [];
        RenderRevision++;
        Message = null;
    }

    private void Deny() {
        Clear();
        Retired();
        Changed();
    }

    private void CancelReviewRead() {
        reviewGeneration++;
        reviewRead?.Cancel();
        reviewRead = null;
        ReadingReview = false;
    }

    private void RetireObservation() {
        CancelReviewRead();
        generation++;
        initialized = false;
        var previous = observation;
        observation = null;
        personId = null;
        if (previous is not null) {
            retirement = Task.WhenAll(retirement, previous.DisposeAsync().AsTask());
        }
    }

    private bool IsCurrent(long opening, Guid? target) => !disposed && opening == generation && personId == target && target.HasValue;
    private static Result Conflict() => Result.Failure(Error.Failure("Review the saved affiliations before saving this draft.", CrmHrEditingErrorCodes.AffiliationConflict));

    public async ValueTask DisposeAsync() {
        disposed = true;
        Clear();
        await retirement;
    }
}

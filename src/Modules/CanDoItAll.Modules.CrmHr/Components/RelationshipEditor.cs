using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record RelationshipRead(Guid PartyId, IReadOnlyList<PartyRelationshipListItemModel> Rows, long Opening, long HintRevision);
public sealed record RelationshipSubmission(Guid PartyId, IReadOnlyList<PartyRelationshipEditorModel> Rows, PartyRelationshipObservation Original, long Opening);

public sealed class RelationshipEditor(IPartyRelationshipService relationships, CrmHrChanges changes, EditingChangeFeed feed,
    IEditingUiAccess access, ILogger<EditingObservation> observationLog, ILogger<RelationshipEditor> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private Guid? partyId;
    private Guid editorId = Guid.NewGuid();
    private readonly Queue<Guid> operations = new();
    private long generation;
    private long hintRevision;
    private long reviewGeneration;
    private CancellationTokenSource? reviewRead;
    private bool initialized;
    private bool disposed;
    public RelationshipReconciliation State { get; } = new();
    public IReadOnlyList<PartyRelationshipListItemModel> Saved { get; private set; } = [];
    public long RenderRevision { get; private set; }
    public bool ReadingReview { get; private set; }
    public string? Message { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }

    public async Task<RelationshipRead> ReadAsync(Guid id, CancellationToken cancellationToken = default) {
        if (observation is null || partyId != id) {
            RetireObservation();
            var opening = generation;
            await retirement;
            if (disposed || opening != generation) {
                throw new OperationCanceledException("The relationship opening was superseded.");
            }
            partyId = id;
            initialized = false;
            editorId = Guid.NewGuid();
            operations.Clear();
            var current = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmHrRelationships, id), observationLog);
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
        var rows = await relationships.ListAsync(id, reading.Token);
        await source.AuthorizeAsync(write: false, reading.Token);
        return new(id, rows, request, revision);
    }

    public void Observe(RelationshipRead read) {
        if (!IsCurrent(read.Opening, read.PartyId)) {
            return;
        }
        if (!initialized) {
            State.Reset(read.PartyId, read.Rows);
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

    public RelationshipSubmission PrepareSave() {
        if (ReadingReview || !initialized || partyId is not Guid id || observation is null) {
            throw new InvalidOperationException("Open the relationship owner before saving.");
        }
        return new(id, State.BeginSave(), State.Original, generation);
    }

    public void CancelSubmission(RelationshipSubmission submitted) {
        if (IsCurrent(submitted.Opening, submitted.PartyId)) {
            State.Rejected(conflict: false);
        }
    }

    public async Task<Result> CommitAsync(RelationshipSubmission submitted) {
        if (!IsCurrent(submitted.Opening, submitted.PartyId)) {
            return Conflict();
        }
        if (observation is not { } current || !State.IsSaving || State.IsOutdated) {
            State.Rejected(conflict: true);
            return Conflict();
        }
        PartyRelationshipSaveReceipt? accepted = null;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (!IsCurrent(submitted.Opening, submitted.PartyId)) {
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
            var result = await relationships.ReplaceAsync(submitted.PartyId, submitted.Rows, submitted.Original, current.Actor,
                onCommitted: receipt => accepted = CaptureReceipt(receipt, submitted.PartyId));
            if (IsCurrent(submitted.Opening, submitted.PartyId)) {
                if (result.IsSuccess) {
                    accepted ??= CaptureReceipt(result.Value!, submitted.PartyId);
                    Accept(submitted, accepted.Accepted);
                    Message = null;
                } else {
                    State.Rejected(result.Errors.Any(error => error.Code is CrmHrEditingErrorCodes.RelationshipConflict));
                    Message = string.Join(" ", result.Errors.Select(error => error.Message));
                }
            }
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Errors);
        } catch (UnauthorizedAccessException) when (accepted is null) {
            if (IsCurrent(submitted.Opening, submitted.PartyId)) {
                Deny();
            }
            return Result.Failure(Error.Failure("Current relationship editing access is required.", CrmHrEditingErrorCodes.EditingAccessDenied));
        } catch (Exception exception) {
            logger.LogWarning(exception, "Relationship continuation failed for party {PartyId}; accepted {Accepted}.", submitted.PartyId, accepted is not null);
            if (IsCurrent(submitted.Opening, submitted.PartyId)) {
                if (accepted is not null) {
                    Accept(submitted, accepted.Accepted);
                    Message = "Relationships were saved. A follow-up failed; the accepted write will not be repeated.";
                } else {
                    State.Unknown(submitted.Rows);
                    Message = "The relationship save outcome is unknown. Its submission and your draft are retained; resubmission is blocked.";
                }
            }
            return accepted is not null ? Result.Success() : Result.Failure(Error.Failure("The relationship save outcome is unknown.", CrmHrEditingErrorCodes.RelationshipOutcomeUnknown));
        } finally {
            if (IsCurrent(submitted.Opening, submitted.PartyId)) {
                Changed();
            }
        }
    }

    private void Accept(RelationshipSubmission submitted, IReadOnlyList<PartyRelationshipListItemModel> accepted) {
        State.Accepted(submitted.Rows, accepted);
        Saved = accepted;
        RenderRevision++;
    }

    private static PartyRelationshipSaveReceipt CaptureReceipt(PartyRelationshipSaveReceipt receipt, Guid partyId) =>
        receipt.PartyId == partyId ? receipt with { Accepted = receipt.Accepted.ToArray() } :
            throw new InvalidOperationException("The relationship receipt belongs to a different party.");

    public async Task ReviewAsync() {
        if (ReadingReview || State.IsSaving || observation is not { } current || partyId is not Guid id) {
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
            var saved = await relationships.ListAsync(id, reading.Token);
            await current.AuthorizeAsync(write: false, reading.Token);
            if (IsCurrent(opening, id) && attempt == reviewGeneration) {
                State.Compare(saved, revision);
                Message = null;
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening, id)) {
                Deny();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening, id) || attempt != reviewGeneration) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Relationship comparison failed for party {PartyId}.", id);
            if (IsCurrent(opening, id) && attempt == reviewGeneration) {
                Message = "Saved relationships could not be read. Your draft and comparison choices are retained.";
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
        if (!IsCurrent(opening, partyId) || observation is not { } current ||
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
                var saved = await relationships.ListAsync(partyId!.Value, current.Lifetime, maximumRows: PartyMergeReview.DetailedRowLimit);
                await current.AuthorizeAsync(write: false, current.Lifetime);
                if (!IsCurrent(opening, partyId)) {
                    return;
                }
                State.Observe(saved);
                Saved = saved;
                RenderRevision++;
            } catch (RelationshipReadLimitExceededException) {
                if (!IsCurrent(opening, partyId)) {
                    return;
                }
                Message = "Saved relationships changed. This large set requires an explicit complete comparison; your draft is retained.";
            } catch (UnauthorizedAccessException) {
                throw;
            } catch (OperationCanceledException) when (disposed || opening != generation) {
                return;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Relationship observation failed for party {PartyId}.", partyId);
                if (disposed || opening != generation) {
                    return;
                }
                Message = "Saved relationships could not be refreshed. Your draft is retained; retry the comparison.";
            }
        }
        if (State.IsOutdated) {
            Notice();
        }
        Changed();
    }

    public void Clear() {
        RetireObservation();
        State.Reset(Guid.Empty, []);
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
        partyId = null;
        if (previous is not null) {
            retirement = Task.WhenAll(retirement, previous.DisposeAsync().AsTask());
        }
    }

    private bool IsCurrent(long opening, Guid? target) => !disposed && opening == generation && partyId == target && target.HasValue;
    private static Result Conflict() => Result.Failure(Error.Failure("Review the saved relationships before saving this draft.", CrmHrEditingErrorCodes.RelationshipConflict));

    public async ValueTask DisposeAsync() {
        disposed = true;
        Clear();
        await retirement;
    }
}

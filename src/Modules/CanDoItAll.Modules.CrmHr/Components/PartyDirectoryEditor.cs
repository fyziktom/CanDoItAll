using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record PartyDirectoryRead(PartyEditorModel? Content, long Opening, long HintRevision);

public sealed class PartyDirectoryEditor(PartyDirectoryService parties, CrmHrChanges changes,
    EditingChangeFeed feed, IEditingUiAccess access, ILogger<EditingObservation> observationLog,
    ILogger<PartyDirectoryEditor> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Guid? observedTarget;
    private Guid editorId = Guid.NewGuid();
    private readonly Queue<Guid> ownOperations = new();
    private long generation;
    private bool disposed;
    private bool waitingForInitialRead;
    private bool hintedWhileOpening;
    private long deliveredRevision;
    private long reviewGeneration;
    private CancellationTokenSource? reviewRead;
    private bool inputProblem;
    public PartyEditorViewModel Draft { get; private set; } = PartyEditorViewModel.CreateNew();
    public PartyEditorReconciliation State { get; } = new();
    public string? Message { get; private set; }
    public bool AccessRetired { get; private set; }
    public bool ReadingReview { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }
    public bool IsOwn(EditingChange change) => change.OriginEditorId == editorId && ownOperations.Contains(change.OperationId);

    public void ReportInputProblem() {
        inputProblem = true;
        Message = "Complete the invalid input or finish the open contact editor before comparing or saving. Your draft is retained.";
        Changed();
    }

    public void ConfirmParsedInput() {
        State.ConfirmParsedInput();
        if (inputProblem) {
            Message = null;
            inputProblem = false;
        }
    }

    public async Task ObserveTargetAsync(Guid? target) {
        if (observation is not null && observedTarget == target) {
            await observation.AuthorizeAsync(write: false, default);
            return;
        }
        var opening = ++generation;
        var previous = observation;
        observation = null;
        if (previous is not null) {
            await previous.DisposeAsync();
        }
        if (disposed || opening != generation) {
            throw new OperationCanceledException("This party opening was superseded.");
        }
        var current = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmHrParty, target), observationLog);
        observation = current;
        observedTarget = target;
        waitingForInitialRead = StateOriginalMissing || Draft.Id != target;
        hintedWhileOpening = false;
        AccessRetired = false;
        ReadingReview = false;
        try {
            await current.StartAsync(batch => Dispatch(() => ReceiveAsync(batch, opening)), () => Dispatch(() => {
                if (!disposed && opening == generation) {
                    RetireAccess();
                }
                return Task.CompletedTask;
            }));
        } catch (UnauthorizedAccessException) {
            if (opening == generation) {
                await DenyAsync();
            }
            throw;
        }
    }

    public void Observe(PartyEditorViewModel owner, PartyDirectoryRead? read) {
        if (read is not null && read.Opening != generation) {
            return;
        }
        owner.EnsureChildIdentities();
        if (Draft.Id != owner.Id || StateOriginalMissing) {
            Draft = owner;
            State.Reset(Draft, read?.Content);
            StateOriginalMissing = false;
            editorId = Guid.NewGuid();
            ownOperations.Clear();
        } else {
            State.Observe(Draft, read?.Content ?? owner.ToEditorModel());
        }
        waitingForInitialRead = false;
        if (hintedWhileOpening) {
            State.Hint();
            hintedWhileOpening = false;
        }
        if (read is not null && read.HintRevision != deliveredRevision) {
            State.Hint();
        }
    }

    private bool StateOriginalMissing { get; set; } = true;

    public async Task<PartyDirectoryRead> ReadAsync(Guid target) {
        try {
            await ObserveTargetAsync(target);
            var current = observation!;
            var opening = generation;
            var revision = deliveredRevision;
            await current.AuthorizeAsync(write: false, default);
            var result = await parties.GetPartyAsync(target, current.Lifetime);
            await current.AuthorizeAsync(write: false, default);
            return new(result, opening, revision);
        } catch (UnauthorizedAccessException) {
            if (!AccessRetired) {
                await DenyAsync();
            }
            throw;
        }
    }

    public async Task NewDraftAsync() {
        await RetireObservationAsync();
        Draft = PartyEditorViewModel.CreateNew();
        State.Reset(Draft);
        StateOriginalMissing = false;
        editorId = Guid.NewGuid();
        ownOperations.Clear();
        Message = null;
    }

    public async Task<Result<Guid>> SaveAsync() {
        Message = null;
        if (ReadingReview || State.IsSaving || State.IsOutdated || State.IsUnknown || State.Review is not null) {
            return Result<Guid>.Failure(Error.Failure("Review the saved changes before saving this draft.", CrmHrEditingErrorCodes.PartyConflict));
        }
        try {
            await ObserveTargetAsync(Draft.Id);
            await observation!.AuthorizeAsync(write: true, default);
        } catch (UnauthorizedAccessException) {
            if (!AccessRetired) {
                await DenyAsync();
            }
            return Result<Guid>.Failure(Error.Failure("Current CRM editing access is required.", CrmHrEditingErrorCodes.EditingAccessDenied));
        }
        var opening = generation;
        var draft = Draft;
        var currentObservation = observation!;
        if (ReadingReview || State.IsSaving || State.IsUnknown || State.IsOutdated || State.Review is not null ||
            State.HasValidationErrors || State.HasPendingDateInput || State.HasPendingChildInput) {
            return Result<Guid>.Failure(Error.Failure("The draft changed while its access was checked. Review or complete its input before saving.", CrmHrEditingErrorCodes.PartyConflict));
        }
        var submitted = State.BeginSave(draft);
        Changed();
        submitted.LastChangedBy = currentObservation.Actor;
        var operation = Guid.NewGuid();
        ownOperations.Enqueue(operation);
        while (ownOperations.Count > 32) {
            ownOperations.Dequeue();
        }
        using var origin = changes.Enter(new(editorId, operation));
        using var authority = changes.EnterUiWrite(token => currentObservation.AuthorizeAsync(write: true, token));
        PartySaveReceipt? accepted = null;
        try {
            var result = await parties.SavePartyWithReceiptAsync(submitted, receipt => accepted = receipt);
            if (!IsCurrent(opening, draft)) {
                return result.IsSuccess ? Result<Guid>.Success(result.Value!.PartyId) : Result<Guid>.Failure(result.Errors);
            }
            if (result.IsFailure) {
                State.Rejected(result.Errors.Any(error => error.Code == CrmHrEditingErrorCodes.PartyConflict));
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                return Result<Guid>.Failure(result.Errors);
            }
            await AcceptAsync(draft, submitted, result.Value!);
            return Result<Guid>.Success(result.Value!.PartyId);
        } catch (UnauthorizedAccessException) when (accepted is null) {
            await DenyAsync();
            return Result<Guid>.Failure(Error.Failure("CRM editing authority changed before commit.", CrmHrEditingErrorCodes.EditingAccessDenied));
        } catch (Exception exception) {
            logger.LogWarning(exception, "Party save continuation failed; accepted {Accepted}, target {PartyId}.", accepted is not null, submitted.Id);
            if (IsCurrent(opening, draft)) {
                if (accepted is not null) {
                    await AcceptAsync(draft, submitted, accepted);
                    Message = "The party was saved. A follow-up projection failed; the accepted save will not be repeated.";
                } else {
                    State.Unknown(submitted);
                    Message = "The save outcome is unknown. This draft is retained and cannot be resubmitted automatically.";
                }
            }
            return accepted is not null
                ? Result<Guid>.Success(accepted.PartyId)
                : Result<Guid>.Failure(Error.Failure(Message ?? "The party save outcome requires observation.", CrmHrEditingErrorCodes.PartyOutcomeUnknown));
        }
    }

    private async Task AcceptAsync(PartyEditorViewModel draft, PartyEditorModel submitted, PartySaveReceipt receipt) {
        State.Accepted(draft, submitted, receipt);
        if (observedTarget == receipt.PartyId) {
            return;
        }
        try {
            await ObserveTargetAsync(receipt.PartyId);
        } catch (OperationCanceledException) when (disposed || !ReferenceEquals(Draft, draft)) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Saved party could not attach its exact observation; target {PartyId}.", receipt.PartyId);
            if (!disposed && ReferenceEquals(Draft, draft)) {
                await RetireObservationAsync();
                Message = "The party was saved, but live updates could not attach. Reopen it to restore live observation. The accepted save will not be repeated.";
            }
        }
    }

    public async Task ReviewAsync() {
        if (ReadingReview || State.IsSaving || Draft.Id is not Guid id || observation is null) {
            return;
        }
        var opening = generation;
        var draft = Draft;
        var currentObservation = observation;
        var attempt = ++reviewGeneration;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(currentObservation.Lifetime);
        reviewRead = reading;
        var revision = State.BeginReview();
        ReadingReview = true;
        try {
            await currentObservation.AuthorizeAsync(write: false, default);
            var current = await parties.GetPartyAsync(id, reading.Token);
            await currentObservation.AuthorizeAsync(write: false, default);
            if (!IsCurrent(opening, draft) || attempt != reviewGeneration) {
                return;
            }
            if (current is null) {
                Message = "This party was deleted. Your local draft is retained; saving cannot recreate the deleted record.";
                State.Hint();
                return;
            }
            State.Compare(draft, current, revision);
            Message = null;
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening, draft)) {
                await DenyAsync();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening, draft) || attempt != reviewGeneration) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Party comparison could not read its current owner; target {PartyId}.", id);
            if (IsCurrent(opening, draft) && attempt == reviewGeneration) {
                Message = "The saved record could not be read. Your draft and review choices are retained.";
            }
        } finally {
            if (opening == generation && attempt == reviewGeneration) {
                ReadingReview = false;
                reviewRead = null;
                Changed();
            }
        }
    }

    public bool ApplyReview() {
        var applied = !ReadingReview && State.Apply(Draft);
        if (!applied) {
            Message = "The comparison or local draft changed. Refresh the comparison before applying.";
        }
        Changed();
        return applied;
    }

    public void CancelReview() {
        CancelReviewRead();
        State.CancelReview();
        Changed();
    }

    private void CancelReviewRead() {
        reviewGeneration++;
        reviewRead?.Cancel();
        reviewRead = null;
        ReadingReview = false;
    }

    private async Task ReceiveAsync(EditingChangeBatch batch, long opening) {
        if (disposed || opening != generation || observation is null) {
            return;
        }
        if (!batch.RequiresResync && batch.Changes.All(change => IsOwn(change) || change.Kind == EditingChangeKind.MetadataChanged)) {
            return;
        }
        deliveredRevision++;
        if (waitingForInitialRead) {
            hintedWhileOpening = true;
            return;
        }
        State.Hint();
        if (!State.IsSaving && !State.IsDirty(Draft) && State.Review is null && Draft.Id is Guid id) {
            var draft = Draft;
            var currentObservation = observation;
            try {
                await currentObservation.AuthorizeAsync(write: false, default);
                var current = await parties.GetPartyAsync(id, currentObservation.Lifetime);
                await currentObservation.AuthorizeAsync(write: false, default);
                if (!IsCurrent(opening, draft)) {
                    return;
                }
                if (current is not null) {
                    State.Observe(draft, current);
                }
            } catch (UnauthorizedAccessException) {
                throw;
            } catch (OperationCanceledException) when (!IsCurrent(opening, draft)) {
                return;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Party observation could not refresh its owner; target {PartyId}.", id);
                if (!IsCurrent(opening, draft)) {
                    return;
                }
                Message = "The saved party could not be refreshed. Your draft is retained; retry the comparison.";
            }
        }
        if (State.IsOutdated) {
            Notice();
        }
        Changed();
    }

    private bool IsCurrent(long opening, PartyEditorViewModel draft) => !disposed && opening == generation && ReferenceEquals(Draft, draft);

    private void RetireAccess() {
        generation++;
        AccessRetired = true;
        Draft = PartyEditorViewModel.CreateNew();
        State.Reset(Draft);
        StateOriginalMissing = true;
        Retired();
        Changed();
    }

    private async Task DenyAsync() {
        await RetireObservationAsync();
        RetireAccess();
    }

    private async Task RetireObservationAsync() {
        CancelReviewRead();
        generation++;
        var previous = observation;
        observation = null;
        observedTarget = null;
        if (previous is not null) {
            await previous.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        await RetireObservationAsync();
    }
}

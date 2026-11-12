using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record AccountProfileRead(CrmAccountWorkspaceModel? Workspace, long Opening, long HintRevision);

public sealed class AccountProfileEditor(CrmService crm, CrmHrChanges changes, EditingChangeFeed feed,
    IEditingUiAccess access, ILogger<EditingObservation> observationLog, ILogger<AccountProfileEditor> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private Guid? observedTarget;
    private Guid editorId = Guid.NewGuid();
    private readonly Queue<Guid> operations = new();
    private long generation;
    private long hintRevision;
    private long reviewGeneration;
    private CancellationTokenSource? reviewRead;
    private bool disposed;
    public CrmAccountProfileEditorModel Draft { get; private set; } = new();
    public AccountProfileReconciliation State { get; } = new();
    public string? Message { get; private set; }
    public bool ReadingReview { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }
    public bool IsOwn(EditingChange change) => change.OriginEditorId == editorId && operations.Contains(change.OperationId);
    public string Actor => observation?.Actor ?? throw new InvalidOperationException("Open the account with current access first.");

    public ValueTask AuthorizeWriteAsync(Guid accountPartyId, CancellationToken cancellationToken) {
        if (disposed || observedTarget != accountPartyId || observation is not { } current) {
            throw new UnauthorizedAccessException("The account write no longer belongs to its authorized opening.");
        }
        return current.AuthorizeAsync(write: true, cancellationToken);
    }

    public async Task AuthorizeCatalogReadAsync(CancellationToken cancellationToken) {
        using var grant = await access.OpenAsync(new(changes.Scope, EditingOwner.CrmAccountProfile), cancellationToken);
        await access.ValidateAsync(grant, write: false, cancellationToken);
    }

    public async Task<AccountProfileRead> ReadWorkspaceAsync(Guid id, CancellationToken cancellationToken) {
        await OpenAsync(id);
        var current = observation!;
        var opening = generation;
        var revision = hintRevision;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, current.Lifetime);
        await current.AuthorizeAsync(write: false, reading.Token);
        var account = await crm.GetAccountWorkspaceAsync(id, reading.Token);
        await current.AuthorizeAsync(write: false, reading.Token);
        return new(account, opening, revision);
    }

    public void Observe(AccountProfileRead read) {
        if (disposed || read.Opening != generation || read.Workspace is not { } account) {
            return;
        }
        if (Draft.AccountPartyId != account.AccountPartyId) {
            Draft = account.Profile.Snapshot();
            State.Reset(Draft);
            editorId = Guid.NewGuid();
            operations.Clear();
        } else {
            State.Observe(Draft, account.Profile);
        }
        if (read.HintRevision != hintRevision) {
            State.Hint();
        }
    }

    public void Missing() {
        State.Hint();
        Message = "This account is unavailable. Your local draft is retained; saving cannot recreate a deleted account.";
        Changed();
    }

    private async Task OpenAsync(Guid id) {
        if (observation is not null && observedTarget == id) {
            await observation.AuthorizeAsync(write: false, default);
            return;
        }
        RetireObservation();
        var opening = generation;
        await retirement;
        if (disposed || opening != generation) {
            throw new OperationCanceledException("The account opening was superseded.");
        }
        observedTarget = id;
        var current = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmAccountProfile, id), observationLog);
        observation = current;
        await current.StartAsync(batch => Dispatch(() => ReceiveAsync(batch, opening)), () => Dispatch(() => {
            if (!disposed && opening == generation) {
                Deny();
            }
            return Task.CompletedTask;
        }));
    }

    public async Task<Result<Guid>> SaveAsync() {
        if (ReadingReview || State.IsSaving || State.IsUnknown || State.IsOutdated || State.Review is not null) {
            return Blocked();
        }
        var draft = Draft;
        var opening = generation;
        var current = observation;
        if (current is null || draft.AccountPartyId == Guid.Empty) {
            return Blocked();
        }
        try {
            await current.AuthorizeAsync(write: true, default);
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening, draft)) {
                Deny();
            }
            return Result<Guid>.Failure(Error.Failure("Current CRM editing access is required.", CrmHrEditingErrorCodes.EditingAccessDenied));
        }
        if (!IsCurrent(opening, draft) || ReadingReview || State.IsSaving || State.IsUnknown || State.IsOutdated || State.Review is not null) {
            return Blocked();
        }
        Message = null;
        var submitted = State.BeginSave(draft);
        Changed();
        submitted.LastChangedBy = current.Actor;
        var operation = Guid.NewGuid();
        operations.Enqueue(operation);
        while (operations.Count > 32) {
            operations.Dequeue();
        }
        using var origin = changes.Enter(new(editorId, operation));
        using var authority = changes.EnterUiWrite(token => current.AuthorizeAsync(write: true, token));
        CrmAccountProfileSaveReceipt? accepted = null;
        try {
            var result = await crm.SaveAccountProfileWithReceiptAsync(submitted, receipt => accepted = receipt);
            if (IsCurrent(opening, draft)) {
                if (result.IsSuccess) {
                    State.Accepted(draft, submitted, result.Value!);
                } else {
                    State.Rejected(result.Errors.Any(error => error.Code == CrmHrEditingErrorCodes.AccountConflict));
                    Message = string.Join(" ", result.Errors.Select(error => error.Message));
                }
            }
            return result.IsSuccess ? Result<Guid>.Success(result.Value!.AccountPartyId) : Result<Guid>.Failure(result.Errors);
        } catch (UnauthorizedAccessException) when (accepted is null) {
            if (IsCurrent(opening, draft)) {
                Deny();
            }
            return Result<Guid>.Failure(Error.Failure("CRM editing authority changed before commit.", CrmHrEditingErrorCodes.EditingAccessDenied));
        } catch (Exception exception) {
            logger.LogWarning(exception, "Account profile continuation failed; accepted {Accepted}, account {AccountId}.", accepted is not null, submitted.AccountPartyId);
            if (IsCurrent(opening, draft)) {
                if (accepted is not null) {
                    State.Accepted(draft, submitted, accepted);
                    Message = "The profile was saved. A follow-up projection failed; the accepted save will not be repeated.";
                } else {
                    State.Unknown(submitted);
                    Message = "The profile save outcome is unknown. Its submission and your later draft are retained; resubmission is blocked.";
                }
            }
            return accepted is not null ? Result<Guid>.Success(accepted.AccountPartyId)
                : Result<Guid>.Failure(Error.Failure("The account profile save outcome is unknown.", CrmHrEditingErrorCodes.AccountOutcomeUnknown));
        } finally {
            if (IsCurrent(opening, draft)) {
                Changed();
            }
        }
    }

    public async Task ReviewAsync() {
        if (ReadingReview || State.IsSaving || observation is not { } current || Draft.AccountPartyId == Guid.Empty) {
            return;
        }
        var opening = generation;
        var draft = Draft;
        var revision = State.BeginReview();
        var attempt = ++reviewGeneration;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(current.Lifetime);
        reviewRead = reading;
        ReadingReview = true;
        try {
            await current.AuthorizeAsync(write: false, default);
            var saved = await crm.GetAccountProfileAsync(draft.AccountPartyId, reading.Token);
            await current.AuthorizeAsync(write: false, default);
            if (!IsCurrent(opening, draft) || attempt != reviewGeneration) {
                return;
            }
            if (saved is null) {
                Missing();
                return;
            }
            State.Compare(draft, saved, revision);
            Message = null;
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening, draft)) {
                Deny();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening, draft) || attempt != reviewGeneration) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Account profile comparison could not read account {AccountId}.", draft.AccountPartyId);
            if (IsCurrent(opening, draft) && attempt == reviewGeneration) {
                Message = "The saved profile could not be read. Your draft and comparison choices are retained.";
            }
        } finally {
            if (opening == generation && attempt == reviewGeneration) {
                ReadingReview = false;
                reviewRead = null;
                Changed();
            }
        }
    }

    private async Task ReceiveAsync(EditingChangeBatch batch, long opening) {
        if (disposed || opening != generation || observation is not { } current ||
            !batch.RequiresResync && batch.Changes.All(change => change.OriginEditorId == editorId && operations.Contains(change.OperationId))) {
            return;
        }
        hintRevision++;
        if (Draft.AccountPartyId != observedTarget) {
            return;
        }
        State.Hint();
        var draft = Draft;
        if (!State.IsSaving && !State.IsUnknown && State.Review is null && !State.IsDirty(draft)) {
            try {
                await current.AuthorizeAsync(write: false, default);
                var saved = await crm.GetAccountProfileAsync(draft.AccountPartyId, current.Lifetime);
                await current.AuthorizeAsync(write: false, default);
                if (!IsCurrent(opening, draft)) {
                    return;
                }
                if (saved is not null) {
                    State.Observe(draft, saved);
                } else {
                    Missing();
                }
            } catch (UnauthorizedAccessException) {
                throw;
            } catch (OperationCanceledException) when (!IsCurrent(opening, draft)) {
                return;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Account profile observation could not read account {AccountId}.", draft.AccountPartyId);
                if (!IsCurrent(opening, draft)) {
                    return;
                }
                Message = "The saved profile could not be refreshed. Your draft is retained; retry the comparison.";
            }
        }
        if (State.IsOutdated) {
            Notice();
        }
        Changed();
    }

    public void Clear() {
        RetireObservation();
        Draft = new();
        State.Reset(Draft);
        operations.Clear();
        ReadingReview = false;
        Message = null;
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

    private void Deny() {
        Clear();
        Retired();
        Changed();
    }

    private void RetireObservation() {
        CancelReviewRead();
        generation++;
        var previous = observation;
        observation = null;
        observedTarget = null;
        if (previous is not null) {
            retirement = Task.WhenAll(retirement, previous.DisposeAsync().AsTask());
        }
    }

    private bool IsCurrent(long opening, CrmAccountProfileEditorModel draft) => !disposed && opening == generation && ReferenceEquals(Draft, draft);
    private static Result<Guid> Blocked() => Result<Guid>.Failure(Error.Failure("Review the saved account profile before saving your draft.", CrmHrEditingErrorCodes.AccountConflict));

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Clear();
        await retirement;
    }
}

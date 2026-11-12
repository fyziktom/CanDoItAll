using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed class OpportunityEditingSession(ICrmOpportunityEditorService owner,
    Func<Guid, CancellationToken, Task<ProjectWriteAdmission?>> observeProject, CrmHrChanges changes, EditingChangeFeed feed,
    IEditingUiAccess access, ILogger<EditingObservation> observationLog, ILogger<OpportunityEditingSession> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private readonly Queue<Guid> operations = new();
    private readonly Dictionary<Guid, string> partyNames = [];
    private readonly Dictionary<ProjectWriteAdmission, string> projectNames = [];
    private Guid editorId = Guid.NewGuid();
    private long generation;
    private long hintRevision;
    private long reviewGeneration;
    private long projectGeneration;
    private CancellationTokenSource? reviewRead;
    private bool disposed;
    public OpportunityReconciliation State { get; } = new();
    public CrmOpportunityEditorModel Draft => State.Draft;
    public CrmOpportunityDetailModel? Saved { get; private set; }
    public IReadOnlyDictionary<Guid, string> PartyNames => partyNames;
    public IReadOnlyDictionary<ProjectWriteAdmission, string> ProjectNames => projectNames;
    public long Opening => generation;
    public bool ReadingReview { get; private set; }
    public bool CanSave => observation is not null && !ReadingReview && State.CanSave;
    public string? Message { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action Notice { get; init; }

    public async Task<bool> OpenAsync(CrmOpportunityEditorModel initial) {
        Clear();
        var opening = generation;
        await retirement;
        if (!IsCurrent(opening)) {
            return false;
        }
        State.Reset(initial);
        editorId = Guid.NewGuid();
        operations.Clear();
        try {
            await SubscribeAsync(initial.Id, opening);
            if (initial.Id is Guid id && observation is { } current) {
                var saved = await ReadAsync(current, id, default);
                if (IsCurrent(opening)) {
                    Observe(saved);
                }
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Deny();
            }
            return false;
        } catch (OperationCanceledException) when (!IsCurrent(opening)) {
            return false;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Opportunity opening read failed for {OpportunityId}.", initial.Id);
            if (IsCurrent(opening)) {
                State.Hint();
                Message = "The saved opportunity could not be read. Its initial draft is retained; read a current comparison before saving.";
            }
        }
        Changed();
        return IsCurrent(opening);
    }

    private async Task SubscribeAsync(Guid? id, long opening) {
        var source = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmOpportunity, id), observationLog);
        observation = source;
        await source.StartAsync(batch => Dispatch(() => ReceiveAsync(batch, opening)), () => Dispatch(() => {
            if (IsCurrent(opening)) {
                Deny();
            }
            return Task.CompletedTask;
        }));
    }

    private async Task<CrmOpportunityDetailModel?> ReadAsync(EditingObservation source, Guid id, CancellationToken cancellationToken, int? budget = null) {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(source.Lifetime, cancellationToken);
        await source.AuthorizeAsync(write: false, reading.Token);
        var saved = await owner.GetOpportunityForEditingAsync(id, reading.Token, budget);
        await source.AuthorizeAsync(write: false, reading.Token);
        return saved;
    }

    private void Observe(CrmOpportunityDetailModel? saved) {
        if (saved is null) {
            State.Unavailable();
            Message = "This opportunity is unavailable. The draft is retained; saving cannot recreate it.";
            return;
        }
        if (saved.Id != Draft.Id || saved.AccountPartyId != Draft.AccountPartyId) {
            throw new InvalidOperationException("The opportunity read belongs to a different opening.");
        }
        State.Observe(OpportunityEditorDrafts.FromDetail(saved));
        RememberNames(saved);
        Saved = saved;
    }

    private void RememberNames(CrmOpportunityDetailModel saved) {
        partyNames[saved.AccountPartyId] = saved.AccountDisplayName;
        partyNames[saved.OwnerPartyId] = saved.OwnerDisplayName;
        if (saved.DeliveryUnitPartyId is Guid unitId) {
            partyNames[unitId] = saved.DeliveryUnitDisplayName;
        }
        foreach (var row in saved.Parties) {
            partyNames[row.PartyId] = row.DisplayName;
        }
        if (saved.LinkedProjectAdmission is { } project) {
            projectNames[project] = saved.LinkedProjectName;
        }
        var references = State.MetadataReferences();
        foreach (var id in partyNames.Keys.Where(id => !references.Parties.Contains(id)).ToArray()) {
            partyNames.Remove(id);
        }
        foreach (var reference in projectNames.Keys.Where(reference => !references.Projects.Contains(reference)).ToArray()) {
            projectNames.Remove(reference);
        }
    }

    public async Task<Result<Guid>> SaveAsync() {
        if (!CanSave || observation is not { } current) {
            return Conflict();
        }
        var opening = generation;
        var submitted = State.BeginSave();
        var inputRevision = State.InputRevision;
        Message = null;
        Changed();
        CrmOpportunitySaveReceipt? accepted = null;
        try {
            await current.AuthorizeAsync(write: true, default);
            if (!IsCurrent(opening)) {
                return Conflict();
            }
            if (State.IsOutdated) {
                State.Rejected(conflict: true);
                return Conflict();
            }
            submitted.LastChangedBy = current.Actor;
            var operation = Guid.NewGuid();
            operations.Enqueue(operation);
            while (operations.Count > 32) {
                operations.Dequeue();
            }
            using var origin = changes.Enter(new(editorId, operation));
            using var authority = changes.EnterUiWrite(token => current.AuthorizeAsync(write: true, token));
            var result = await owner.SaveOpportunityWithReceiptAsync(submitted, onCommitted: receipt => accepted = Capture(receipt, submitted));
            if (result.IsSuccess) {
                accepted ??= Capture(result.Value!, submitted);
            }
            if (IsCurrent(opening)) {
                if (accepted is not null) {
                    State.Accepted(submitted, accepted.Accepted);
                    if (inputRevision != State.InputRevision) {
                        Message = "The submitted opportunity was saved. Your later input is retained; review it before another save.";
                    }
                    if (result.IsFailure) {
                        Message = "The opportunity was saved; a follow-up reported a failure. The accepted write will not be repeated.";
                    }
                } else {
                    State.Rejected(result.Errors.Any(error => error.Code is CrmHrEditingErrorCodes.OpportunityConflict or CrmHrEditingErrorCodes.OpportunityProjectStale));
                    Message = string.Join(" ", result.Errors.Select(error => error.Message));
                }
            }
            if (accepted is not null && submitted.Id is null && IsCurrent(opening)) {
                await RetargetCreatedAsync(accepted.OpportunityId);
            }
            return accepted is not null ? Result<Guid>.Success(accepted.OpportunityId) : Result<Guid>.Failure(result.Errors);
        } catch (UnauthorizedAccessException) when (accepted is null) {
            if (IsCurrent(opening)) {
                Deny();
            }
            return Result<Guid>.Failure(Error.Failure("Current opportunity editing access is required.", CrmHrEditingErrorCodes.EditingAccessDenied));
        } catch (Exception exception) {
            logger.LogWarning(exception, "Opportunity continuation failed for {OpportunityId}; accepted {Accepted}.", submitted.Id, accepted is not null);
            if (IsCurrent(opening)) {
                if (accepted is not null) {
                    State.Accepted(submitted, accepted.Accepted);
                    Message = "The opportunity was saved. A follow-up failed; the accepted write will not be repeated.";
                    if (submitted.Id is null) {
                        await RetargetCreatedAsync(accepted.OpportunityId);
                    }
                } else {
                    State.Unknown(submitted);
                    Message = "The opportunity save outcome is unknown. Its submission and your draft are retained; resubmission is blocked.";
                }
            }
            return accepted is not null ? Result<Guid>.Success(accepted.OpportunityId) :
                Result<Guid>.Failure(Error.Failure("The opportunity save outcome is unknown.", CrmHrEditingErrorCodes.OpportunityOutcomeUnknown));
        } finally {
            if (!disposed) {
                Changed();
            }
        }
    }

    private static CrmOpportunitySaveReceipt Capture(CrmOpportunitySaveReceipt receipt, CrmOpportunityEditorModel submitted) {
        if (receipt.OpportunityId == Guid.Empty || receipt.Accepted.Id != receipt.OpportunityId ||
            submitted.Id is Guid id && id != receipt.OpportunityId || receipt.Accepted.AccountPartyId != submitted.AccountPartyId ||
            receipt.Accepted.ExpectedUpdatedAtUtc is null) {
            throw new InvalidOperationException("The opportunity receipt does not identify this submission.");
        }
        var captured = receipt with { Accepted = receipt.Accepted.Snapshot() };
        if (captured.Accepted.Parties.Any(row => row.Id is null || row.Id == Guid.Empty) ||
            captured.Accepted.Parties.Select(row => row.Id).Distinct().Count() != captured.Accepted.Parties.Count) {
            throw new InvalidOperationException("The opportunity receipt has invalid linked-party identities.");
        }
        return captured;
    }

    private async Task RetargetCreatedAsync(Guid id) {
        RetireObservation();
        var opening = generation;
        await retirement;
        if (!IsCurrent(opening)) {
            return;
        }
        try {
            await SubscribeAsync(id, opening);
            if (observation is { } current) {
                var saved = await ReadAsync(current, id, default, PartyMergeReview.DetailedRowLimit);
                if (IsCurrent(opening)) {
                    Observe(saved);
                }
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Deny();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Created opportunity observation failed for {OpportunityId}.", id);
            if (IsCurrent(opening)) {
                State.Hint();
                Message = "The opportunity was created. Its current observation failed; the accepted identity and your later draft are retained.";
            }
        }
    }

    public async Task ReviewAsync() {
        if (ReadingReview || State.IsSaving || observation is not { } current || Draft.Id is not Guid id) {
            return;
        }
        var opening = generation;
        var attempt = ++reviewGeneration;
        var revision = State.BeginReview();
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(current.Lifetime);
        reviewRead = reading;
        ReadingReview = true;
        Changed();
        try {
            var saved = await ReadAsync(current, id, reading.Token);
            if (!IsCurrent(opening) || attempt != reviewGeneration) {
                return;
            }
            if (saved is null) {
                Observe(null);
            } else {
                State.Compare(OpportunityEditorDrafts.FromDetail(saved), revision);
                RememberNames(saved);
                Saved = saved;
                Message = null;
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Deny();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening) || attempt != reviewGeneration) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Opportunity comparison failed for {OpportunityId}.", id);
            if (IsCurrent(opening) && attempt == reviewGeneration) {
                Message = "The saved opportunity could not be read. Your draft and comparison choices are retained.";
            }
        } finally {
            if (IsCurrent(opening) && attempt == reviewGeneration) {
                ReadingReview = false;
                reviewRead = null;
                Changed();
            }
        }
    }

    public void ApplyReview() {
        if (!ReadingReview && State.Apply()) {
            Changed();
        }
    }
    public void Undo() {
        State.Undo();
        Changed();
    }
    public void CancelReview() {
        CancelReviewRead();
        State.CancelReview();
        Changed();
    }

    public async Task SelectProjectAsync(Guid id, long expectedOpening) {
        if (!IsCurrent(expectedOpening) || observation is not { } current) {
            return;
        }
        var opening = generation;
        var attempt = ++projectGeneration;
        try {
            await current.AuthorizeAsync(write: false, current.Lifetime);
            var admission = await observeProject(id, current.Lifetime);
            await current.AuthorizeAsync(write: false, current.Lifetime);
            if (!IsCurrent(opening) || attempt != projectGeneration) {
                return;
            }
            if (admission is null) {
                Message = "The selected project is unavailable. Select a current project before saving.";
            } else {
                if (admission.ProjectId != id) {
                    throw new InvalidOperationException("The project observation identifies another selection.");
                }
                Draft.LinkedProjectId = admission.ProjectId;
                Draft.LinkedProjectAdmission = admission;
                State.InputChanged(new(OpportunityField.Project));
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Deny();
            }
        } catch (OperationCanceledException) when (!IsCurrent(opening)) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Opportunity project observation failed for {ProjectId}.", id);
            if (IsCurrent(opening) && attempt == projectGeneration) {
                Message = "The selected project could not be observed. The previous reference and your draft are retained.";
            }
        }
        Changed();
    }

    public void InputChanged(OpportunityInput input) {
        if (input.Field == OpportunityField.Project) {
            projectGeneration++;
        }
        State.InputChanged(input);
    }

    private async Task ReceiveAsync(EditingChangeBatch batch, long opening) {
        if (!IsCurrent(opening) || observation is not { } current || Draft.Id is not Guid id ||
            !batch.RequiresResync && batch.Changes.All(change => change.OriginEditorId == editorId && operations.Contains(change.OperationId))) {
            return;
        }
        var hint = ++hintRevision;
        State.Hint();
        if (!State.IsDirty && !State.IsSaving && !State.IsUnknown && State.Review is null) {
            try {
                var saved = await ReadAsync(current, id, current.Lifetime, PartyMergeReview.DetailedRowLimit);
                if (!IsCurrent(opening)) {
                    return;
                }
                Observe(saved);
                if (hint != hintRevision) {
                    State.Hint();
                }
            } catch (OpportunityReadLimitExceededException) {
                Message = "The linked-party set exceeds the automatic read budget. Request a complete comparison explicitly.";
            } catch (UnauthorizedAccessException) {
                throw;
            } catch (OperationCanceledException) when (!IsCurrent(opening)) {
                return;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Opportunity observation failed for {OpportunityId}.", id);
                if (!IsCurrent(opening)) {
                    return;
                }
                Message = "The saved opportunity could not be refreshed. Your draft is retained; retry its comparison.";
            }
        }
        if (State.IsOutdated) {
            Notice();
        }
        Changed();
    }

    public void Clear() {
        RetireObservation();
        State.Reset(new());
        Saved = null;
        partyNames.Clear();
        projectNames.Clear();
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
        projectGeneration++;
        var previous = observation;
        observation = null;
        if (previous is not null) {
            retirement = Task.WhenAll(retirement, previous.DisposeAsync().AsTask());
        }
    }
    private bool IsCurrent(long opening) => !disposed && generation == opening;
    private static Result<Guid> Conflict() => Result<Guid>.Failure(Error.Failure("Review the saved opportunity before saving.", CrmHrEditingErrorCodes.OpportunityConflict));
    public async ValueTask DisposeAsync() {
        disposed = true;
        Clear();
        await retirement;
    }
}


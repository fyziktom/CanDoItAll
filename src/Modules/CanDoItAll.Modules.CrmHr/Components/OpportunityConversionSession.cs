using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public enum OpportunityConversionNoticeKind { Warning, Error }
public sealed record OpportunityConversionNotice(OpportunityConversionNoticeKind Kind, string Title, string Message);

public sealed class OpportunityConversionSession(ICrmOpportunityConversionService owner, CrmHrChanges changes,
    EditingChangeFeed feed, IEditingUiAccess access, ILogger<EditingObservation> observerLog,
    ILogger<OpportunityConversionSession> logger) : IAsyncDisposable {
    private const int AutomaticPartyLinkBudget = 1000;
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private CancellationTokenSource? read;
    private bool ready;
    private bool disposed;
    public OpportunityConversionState State { get; } = new();
    public bool Reading { get; private set; }
    public bool CanSave => ready && !Reading && State.CanSubmit;
    public string? Message { get; private set; }
    public ProjectWriteAdmission? RecoveryProject { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Action Retired { get; init; }
    public required Action<OpportunityConversionNotice> Notice { get; init; }

    public async Task<bool> OpenAsync(CrmOpportunityDetailModel basis, CrmOpportunityConversionEditorModel draft) {
        if (draft.OpportunityId != basis.Id || draft.ExpectedUpdatedAtUtc != basis.UpdatedAtUtc) {
            throw new ArgumentException("The conversion draft must identify its displayed original opportunity.", nameof(draft));
        }
        Clear();
        State.Open(draft, basis);
        var opening = State.Opening;
        await retirement;
        if (!IsCurrent(opening)) {
            return false;
        }
        var source = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmOpportunity, basis.Id), observerLog);
        observation = source;
        try {
            await source.StartAsync(batch => Dispatch(() => {
                if (IsCurrent(opening) && (batch.RequiresResync || batch.Changes.Any(change => !State.IsOwn(change.OriginEditorId, change.OperationId)))) {
                    var notify = !State.IsOutdated;
                    State.Hint();
                    if (notify && !State.IsUnknown) {
                        SetNotice(OpportunityConversionNoticeKind.Warning, "Conversion basis changed", "Your conversion draft is retained. Review the current opportunity before submitting.");
                    }
                    Changed();
                }
                return Task.CompletedTask;
            }), () => Dispatch(() => {
                if (IsCurrent(opening)) {
                    Retire();
                }
                return Task.CompletedTask;
            }));
            if (!IsCurrent(opening)) {
                return false;
            }
            ready = true;
            CrmOpportunityDetailModel? current;
            using (var initial = BeginRead()) {
                try {
                    await source.AuthorizeAsync(write: false, initial.Token);
                    current = await owner.GetOpportunityForEditingAsync(basis.Id, initial.Token, AutomaticPartyLinkBudget);
                } finally {
                    FinishRead(initial);
                }
            }
            if (!IsCurrent(opening)) {
                return false;
            }
            if (current is null || current.UpdatedAtUtc != basis.UpdatedAtUtc) {
                State.Hint();
                Message = "The displayed conversion basis changed. Review its current state before submitting.";
            } else if (State.Draft.LinkExistingProject && State.Draft.ExpectedProjectAdmission is { } project) {
                await ObserveProjectAsync(project);
            }
        } catch (OperationCanceledException) {
            if (IsCurrent(opening)) {
                State.Hint();
                Message = "The opening snapshot was not confirmed. Your draft is retained; review its current basis before submitting.";
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Retire();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Conversion opening observation failed for {OpportunityId}.", basis.Id);
            if (IsCurrent(opening)) {
                State.Hint();
                Message = "The conversion basis could not be observed. Its draft is retained; request a complete review before submitting.";
            }
        }
        Changed();
        return IsCurrent(opening) && ready;
    }

    public async Task ObserveProjectAsync(ProjectWriteAdmission project) {
        if (!ready || observation is not { } source || Reading) {
            return;
        }
        var opening = State.Opening;
        var version = State.Draft.ExpectedUpdatedAtUtc ?? throw new InvalidOperationException("Conversion has no original opportunity observation.");
        if (State.Draft.ExpectedProjectAdmission != project) {
            return;
        }
        State.Draft.ExpectedProjectBasis = null;
        using var pending = BeginRead();
        try {
            await source.AuthorizeAsync(write: false, pending.Token);
            var result = await owner.ObserveOpportunityProjectBasisAsync(State.Draft.OpportunityId,
                version, project, pending.Token);
            if (!IsCurrent(opening) || pending.IsCancellationRequested || State.Draft.ExpectedProjectAdmission != project ||
                State.Draft.ExpectedUpdatedAtUtc != version) {
                return;
            }
            if (result.IsFailure) {
                State.Hint();
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
            } else {
                State.Draft.ExpectedProjectBasis = CaptureProjectBasis(result.Value, State.Draft.OpportunityId, version, project);
            }
        } catch (OperationCanceledException) when (pending.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Retire();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Conversion target observation failed for {ProjectId}.", project.ProjectId);
            if (IsCurrent(opening)) {
                Message = "The selected project could not be observed. Choose its current record before converting.";
            }
        } finally {
            FinishRead(pending);
        }
    }

    public async Task ReviewAsync() {
        if (!ready || Reading || observation is not { } source) {
            return;
        }
        var opening = State.Opening;
        State.InvalidateReview();
        var hintRevision = State.HintRevision;
        var inputRevision = State.InputRevision;
        var project = State.Draft.LinkExistingProject ? State.Draft.ExpectedProjectAdmission : null;
        using var pending = BeginRead();
        try {
            await source.AuthorizeAsync(write: false, pending.Token);
            var current = await owner.GetOpportunityForEditingAsync(State.Draft.OpportunityId, pending.Token);
            if (!IsCurrent(opening) || pending.IsCancellationRequested) {
                return;
            }
            if (current is null) {
                State.Hint();
                Message = "The opportunity is unavailable. Its conversion draft is retained and cannot be submitted.";
                return;
            }
            CrmOpportunityConversionProjectBasis? target = null;
            var targetUnavailable = State.Draft.LinkExistingProject && project is null;
            if (project is not null) {
                try {
                    var result = await owner.ObserveOpportunityProjectBasisAsync(current.Id, current.UpdatedAtUtc, project, pending.Token);
                    if (result.IsSuccess) {
                        target = CaptureProjectBasis(result.Value, current.Id, current.UpdatedAtUtc, project);
                    } else {
                        targetUnavailable = true;
                        State.Hint();
                        Message = "The source changed while its project basis was read. Refresh this comparison before accepting it.";
                    }
                } catch (ProjectWriteAdmissionRejectedException) {
                    targetUnavailable = true;
                }
            }
            if (IsCurrent(opening) && !pending.IsCancellationRequested) {
                State.SetReview(current, target, targetUnavailable, hintRevision, inputRevision);
            }
        } catch (OperationCanceledException) when (pending.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Retire();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Conversion review failed for {OpportunityId}.", State.Draft.OpportunityId);
            if (IsCurrent(opening)) {
                Message = "The current conversion basis could not be read. Your draft and original observation are retained.";
            }
        } finally {
            FinishRead(pending);
        }
    }

    public async Task ApplyReviewAsync() {
        if (Reading || observation is not { } source) {
            return;
        }
        var opening = State.Opening;
        try {
            await source.AuthorizeAsync(write: true, default);
            if (IsCurrent(opening) && State.ApplyReview()) {
                Message = State.Draft.LinkExistingProject && State.Draft.ExpectedProjectBasis is null
                    ? "The current opportunity basis was accepted. Choose the current project before submitting."
                    : "The current conversion basis was accepted locally. Save conversion remains a separate command.";
                Changed();
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(opening)) {
                Retire();
            }
        }
    }

    public async Task<OpportunityConversionSubmission?> SaveAsync(CrmOpportunityConversionEditorModel model) {
        if (!CanSave || observation is not { } source || model.OpportunityId != State.Draft.OpportunityId ||
            model.ExpectedUpdatedAtUtc != State.Draft.ExpectedUpdatedAtUtc) {
            return null;
        }
        var submitted = State.Begin(model);
        if (submitted is null) {
            return null;
        }
        Message = null;
        Changed();
        CrmOpportunityConversionResult? accepted = null;
        var ownerStarted = false;
        try {
            await source.AuthorizeAsync(write: true, default);
            if (!IsCurrent(submitted.Opening) || State.IsOutdated) {
                return null;
            }
            submitted.Model.LastChangedBy = source.Actor;
            using var origin = changes.Enter(new(State.EditorId, submitted.OperationId));
            using var authority = changes.EnterUiWrite(token => source.AuthorizeAsync(write: true, token));
            ownerStarted = true;
            var result = await owner.ConvertOpportunityToProjectAsync(submitted.Model, onCommitted: receipt => {
                Capture(submitted, receipt);
                accepted = receipt;
            });
            if (!IsCurrent(submitted.Opening)) {
                return null;
            }
            if (result.IsSuccess) {
                var receipt = accepted ?? result.Value ?? throw new InvalidOperationException("Conversion returned success without its committed identity.");
                Capture(submitted, receipt);
                accepted = receipt;
            } else if (accepted is null) {
                if (result.Errors.Any(error => error.Code == CrmOpportunityConversionRequiresObservationException.ErrorCode)) {
                    State.Fail(submitted);
                } else if (result.Errors.Any(error => error.Code is CrmHrEditingErrorCodes.OpportunityConflict or
                    CrmHrEditingErrorCodes.OpportunityConversionConflict or CrmHrEditingErrorCodes.OpportunityConversionBasisRequired or ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot)) {
                    State.Hint();
                }
                SetNotice(OpportunityConversionNoticeKind.Error, "Conversion not saved", string.Join(" ", result.Errors.Select(error => error.Message)));
            }
        } catch (CrmOpportunityConversionRequiresObservationException exception) {
            if (IsCurrent(submitted.Opening)) {
                RecoveryProject = exception.Project;
                State.Fail(submitted);
                SetNotice(OpportunityConversionNoticeKind.Error, "Conversion requires review", exception.Message + " Resubmission is blocked.");
            }
        } catch (ProjectWriteAdmissionRejectedException) {
            if (IsCurrent(submitted.Opening)) {
                State.Hint();
                SetNotice(OpportunityConversionNoticeKind.Error, "Conversion not saved", "The selected project changed after it was chosen. Review its current basis before converting.");
            }
        } catch (UnauthorizedAccessException) when (!ownerStarted) {
            if (IsCurrent(submitted.Opening)) {
                Retire();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Conversion continuation failed for {OpportunityId}; accepted {Accepted}.", submitted.Model.OpportunityId, accepted is not null);
            if (IsCurrent(submitted.Opening)) {
                State.Fail(submitted);
                SetNotice(accepted is null ? OpportunityConversionNoticeKind.Error : OpportunityConversionNoticeKind.Warning,
                    accepted is null ? "Conversion outcome unknown" : "Conversion saved, follow-up failed",
                    accepted is null
                        ? "The conversion outcome is unknown. The submission and draft are retained. Review the opportunity and project state; resubmission is blocked."
                        : "The conversion committed. Its accepted write will not be repeated.");
            }
        } finally {
            State.Complete(submitted);
            if (IsCurrent(submitted.Opening)) {
                Changed();
            }
        }
        return IsCurrent(submitted.Opening) ? submitted : null;
    }

    private void Capture(OpportunityConversionSubmission submission, CrmOpportunityConversionResult receipt) {
        if (receipt.ProjectAdmission is not { } project || project.ProjectId != receipt.ProjectId || receipt.OpportunityUpdatedAtUtc is null) {
            throw new InvalidOperationException("Conversion did not supply its committed project lifetime and opportunity observation.");
        }
        State.Accept(submission, receipt);
    }

    private static CrmOpportunityConversionProjectBasis CaptureProjectBasis(CrmOpportunityConversionProjectBasis? basis,
        Guid opportunityId, DateTimeOffset version, ProjectWriteAdmission project) {
        if (basis is null || basis.OpportunityId != opportunityId || basis.OpportunityUpdatedAtUtc != version || basis.Project != project) {
            throw new InvalidOperationException("The project observation does not identify its requested conversion basis.");
        }
        return basis;
    }

    private CancellationTokenSource BeginRead() {
        read?.Cancel();
        read = new();
        Reading = true;
        Changed();
        return read;
    }

    private void FinishRead(CancellationTokenSource pending) {
        if (ReferenceEquals(read, pending)) {
            read = null;
            Reading = false;
            if (!disposed) {
                Changed();
            }
        }
    }

    public void CancelRead() {
        read?.Cancel();
        read = null;
        Reading = false;
    }

    private void SetNotice(OpportunityConversionNoticeKind kind, string title, string message) {
        Message = message;
        Notice(new(kind, title, message));
    }

    public void RefuseSubmission(string message) => SetNotice(OpportunityConversionNoticeKind.Error, "Conversion not saved", message);

    private bool IsCurrent(long opening) => !disposed && opening == State.Opening;

    private void Retire() {
        Clear();
        Retired();
        SetNotice(OpportunityConversionNoticeKind.Warning, "Conversion access changed", "Reopen this opportunity with current access.");
        Changed();
    }

    public void Clear() {
        CancelRead();
        ready = false;
        Message = null;
        RecoveryProject = null;
        State.Clear();
        var previous = observation;
        observation = null;
        if (previous is not null) {
            retirement = RetireAsync(retirement, previous);
        }
    }

    private static async Task RetireAsync(Task earlier, EditingObservation previous) {
        await earlier;
        await previous.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        Clear();
        disposed = true;
        await retirement;
    }
}

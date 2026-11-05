using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed record RecruitmentPendingDelete(RecruitmentInterviewObservation? Interview, LifecycleTaskObservation? Task);

public sealed class RecruitmentRecordsSession(RecruitingService owner, RecruitmentApplicationSession application,
    CrmHrChanges changes, ILogger<RecruitmentRecordsSession> logger) {
    private long generation;
    public RecruitmentRecordDrafts Drafts { get; } = new();
    public long Epoch { get; private set; }
    public bool IsDeleting { get; private set; }
    public RecruitmentPendingDelete? UnknownDelete { get; private set; }
    public bool IsBusy => Drafts.IsSaving || IsDeleting;
    public bool IsUnknown => Drafts.IsUnknown || UnknownDelete is not null;
    public string? Message { get; private set; }
    public bool RequiresAttention { get; private set; }
    public required Action Changed { get; init; }
    public bool CanWrite => application.State.Draft.Id.HasValue && application.State.Draft.PartyId.HasValue &&
        !IsBusy && !IsUnknown && !application.State.IsSaving && !application.State.IsUnknown && !application.State.Missing;

    public void Open(RecruitmentWorkspaceModel workspace) {
        generation++;
        Epoch++;
        IsDeleting = false;
        UnknownDelete = null;
        Message = null;
        RequiresAttention = false;
        Drafts.Open(workspace);
    }
    public void Clear() {
        generation++;
        Epoch++;
        Drafts.Clear();
        IsDeleting = false;
        UnknownDelete = null;
        Message = null;
        RequiresAttention = false;
    }

    public Task<bool> SaveInterviewAsync() => SaveAsync<RecruitmentInterviewEditorModel, RecruitmentInterviewField, RecruitmentSaveReceipt<RecruitmentInterviewEditorModel>, Guid>(Drafts.Interview, Drafts.Interview.Begin, RecruitmentWriteArea.Interviews,
        (model, receipt) => owner.SaveRecruitmentInterviewAsync(model, onCommitted: receipt), Drafts.AcceptInterview);

    public Task<bool> SaveTaskAsync() => SaveAsync<LifecycleTaskEditorModel, RecruitmentTaskField, RecruitmentSaveReceipt<LifecycleTaskEditorModel>, Guid>(Drafts.Task, Drafts.Task.Begin, RecruitmentWriteArea.Tasks,
        (model, receipt) => owner.SaveLifecycleTaskAsync(model, onCommitted: receipt), Drafts.AcceptTask);

    public Task<bool> SaveSupportAsync() => SaveAsync<RecruitmentSupportAssignmentsEditorModel, RecruitmentSupportField, PartyRelationshipSaveReceipt, PartyRelationshipSaveReceipt>(Drafts.Support, Drafts.BeginSupport, RecruitmentWriteArea.Support,
        (model, receipt) => {
            model.LastChangedBy = application.Actor;
            return owner.SaveSupportAssignmentsAsync(model, onCommitted: receipt);
        }, (submission, receipt) => Drafts.AcceptSupport(submission,
            RecruitmentRecordDrafts.FromSupport(RecruitmentSupportRelationshipPolicy.ToModel(submission.Model.PartyId, receipt.Accepted))));

    public async Task<bool> ConvertAsync() {
        if (!CanWrite || application.State.IsDirty || !application.State.CanSave || Drafts.ConversionOutdated) {
            return false;
        }
        var dependent = application.State.Begin();
        if (dependent is null) {
            return false;
        }
        try {
            return await SaveAsync<RecruitmentConversionEditorModel, RecruitmentConversionField, RecruitmentConversionReceipt, Guid>(Drafts.Conversion, Drafts.BeginConversion, RecruitmentWriteArea.Context,
                (model, receipt) => {
                    model.LastChangedBy = application.Actor;
                    return owner.ConvertCandidateAsync(model, onCommitted: receipt);
                }, (submission, receipt) => {
                    Drafts.AcceptConversion(submission, receipt);
                    application.AcceptConversion(dependent, receipt);
                }, application.CaptureAdmission(RecruitmentWriteArea.Application, write: true), dependentWrite: true);
        } finally {
            if (Drafts.Conversion.IsUnknown) {
                application.State.Unknown(dependent);
            }
            application.State.Complete(dependent);
        }
    }

    private async Task<bool> SaveAsync<TModel, TField, TReceipt, TResult>(RecordDraftState<TModel, TField> state,
        Func<RecordSubmission<TModel, TField>?> begin, RecruitmentWriteArea area,
        Func<TModel, Action<TReceipt>, Task<Result<TResult>>> save,
        Action<RecordSubmission<TModel, TField>, TReceipt> accept,
        Func<CancellationToken, ValueTask>? dependentAdmission = null, bool dependentWrite = false)
        where TModel : class where TField : struct, Enum {
        if (dependentWrite ? IsBusy || IsUnknown : !CanWrite) {
            return false;
        }
        var opening = generation;
        var applicationOpening = application.Opening;
        var admission = application.CaptureAdmission(area, write: true);
        var submission = begin();
        if (submission is null) {
            return false;
        }
        Epoch++;
        var ownerStarted = false;
        var accepted = false;
        Message = null;
        RequiresAttention = false;
        bool Current() => opening == generation && applicationOpening == application.Opening && state.IsCurrent(submission);
        async ValueTask Authorize(CancellationToken token) {
            if (!Current()) {
                throw new UnauthorizedAccessException("The recruitment record opening retired before commit.");
            }
            await admission(token);
            if (dependentAdmission is not null) {
                await dependentAdmission(token);
            }
            if (!Current()) {
                throw new UnauthorizedAccessException("The recruitment record opening retired during authorization.");
            }
        }
        try {
            await Authorize(default);
            using var authority = changes.EnterUiWrite(Authorize);
            using var origin = application.EnterWrite();
            ownerStarted = true;
            var result = await save(submission.Model, receipt => {
                accepted = true;
                if (Current()) {
                    accept(submission, receipt);
                }
            });
            if (!Current()) {
                return false;
            }
            if (result.IsFailure) {
                if (result.Errors.Any(error => error.Code is RecruitmentEditingErrors.Conflict or RecruitmentEditingErrors.ConversionConflict or CrmHrEditingErrorCodes.RelationshipConflict)) {
                    if (area == RecruitmentWriteArea.Support) {
                        Drafts.HintSupport();
                    } else if (area == RecruitmentWriteArea.Context) {
                        Drafts.HintConversion();
                    }
                }
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                return false;
            }
            if (!accepted) {
                throw new InvalidOperationException("The recruitment owner returned success without its committed receipt.");
            }
            Message = state.IsDirty ? "Saved. Later input is retained in your draft." : "Saved.";
            return true;
        } catch (UnauthorizedAccessException exception) {
            logger.LogWarning(exception, "Recruitment {Area} write access retired; accepted {Accepted}.", area, accepted);
            if (Current()) {
                Message = accepted ? "The change committed. Follow-up access failed; the accepted write will not be repeated."
                    : "Current write access is required. Your draft is retained.";
                RequiresAttention = true;
            }
            return accepted;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Recruitment {Area} continuation failed; accepted {Accepted}.", area, accepted);
            if (Current()) {
                if (ownerStarted && !accepted) {
                    state.Unknown(submission);
                }
                Message = state.IsUnknown ? "The save outcome is unknown. Your submission and later draft are retained; resubmission is blocked."
                    : accepted ? "The change committed. Follow-up failed; the accepted write will not be repeated."
                    : "The change was not submitted. Your draft is retained.";
                RequiresAttention = true;
            }
            return accepted;
        } finally {
            state.Complete(submission);
            Epoch++;
            Changed();
        }
    }

    public Task<bool> DeleteInterviewAsync(RecruitmentInterviewItemModel row) => row.ApplicationId != application.State.Draft.Id
        ? Task.FromResult(false) : DeleteAsync(new(row.Original, null), RecruitmentWriteArea.Interviews,
            receipt => owner.DeleteRecruitmentInterviewAsync(row.Id, original: row.Original, onCommitted: receipt));

    public Task<bool> DeleteTaskAsync(LifecycleTaskItemModel row) => row.PartyId != application.State.Draft.PartyId
        ? Task.FromResult(false) : DeleteAsync(new(null, row.Original), RecruitmentWriteArea.Tasks,
            receipt => owner.DeleteLifecycleTaskAsync(row.Id, original: row.Original, onCommitted: receipt));

    private async Task<bool> DeleteAsync(RecruitmentPendingDelete submission, RecruitmentWriteArea area,
        Func<Action<RecruitmentDeleteReceipt>, Task<Result>> delete) {
        if (!CanWrite || submission is { Interview: null, Task: null }) {
            return false;
        }
        var opening = generation;
        var applicationOpening = application.Opening;
        var admission = application.CaptureAdmission(area, write: true);
        var accepted = false;
        var ownerStarted = false;
        IsDeleting = true;
        Epoch++;
        bool Current() => opening == generation && applicationOpening == application.Opening;
        async ValueTask Authorize(CancellationToken token) {
            if (!Current()) {
                throw new UnauthorizedAccessException("The recruitment delete opening retired.");
            }
            await admission(token);
            if (!Current()) {
                throw new UnauthorizedAccessException("The recruitment delete opening retired during authorization.");
            }
        }
        try {
            await Authorize(default);
            using var authority = changes.EnterUiWrite(Authorize);
            using var origin = application.EnterWrite();
            ownerStarted = true;
            var result = await delete(_ => accepted = true);
            if (!Current()) {
                return false;
            }
            if (result.IsFailure) {
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                return false;
            }
            if (!accepted) {
                throw new InvalidOperationException("The recruitment delete returned success without its committed receipt.");
            }
            Message = "Deleted the observed record.";
            RequiresAttention = false;
            return true;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Recruitment {Area} delete failed; accepted {Accepted}.", area, accepted);
            if (Current()) {
                if (ownerStarted && !accepted && exception is not UnauthorizedAccessException) {
                    UnknownDelete = submission;
                }
                Message = accepted ? "The delete committed. Follow-up failed; it will not be repeated."
                    : UnknownDelete is not null ? "The delete outcome is unknown. The observed row is retained; repeating the write is blocked."
                    : "The record was not deleted. Current write access is required.";
                RequiresAttention = true;
            }
            return accepted;
        } finally {
            if (Current()) {
                IsDeleting = false;
                Epoch++;
                Changed();
            }
        }
    }
}

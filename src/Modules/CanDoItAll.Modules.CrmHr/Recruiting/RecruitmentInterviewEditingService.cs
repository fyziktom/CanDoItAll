using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Modules.CrmHr.RecruitmentEditValues;
using static CanDoItAll.Modules.CrmHr.RecruitmentPartyReferences;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitmentInterviewEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IActivityStream activityStream, CrmHrChanges changes) {
    public async Task<Result<Guid>> SaveRecruitmentInterviewAsync(RecruitmentInterviewEditorModel model, CancellationToken cancellationToken = default, Action<RecruitmentSaveReceipt<RecruitmentInterviewEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SaveCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(Conflict());
        }
    }

    private async Task<Result<Guid>> SaveCoreAsync(
        RecruitmentInterviewEditorModel model,
        CancellationToken cancellationToken, Action<RecruitmentSaveReceipt<RecruitmentInterviewEditorModel>> onCommitted) {
        ArgumentNullException.ThrowIfNull(model);

        if (model.ApplicationId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a recruitment application first.", "crmhr.recruiting.interview.application-required"));
        }

        if (!model.ScheduledAtLocal.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Interview schedule is required.", "crmhr.recruiting.interview.schedule-required"));
        }

        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(OriginalRequired());
        }
        if (model.Original?.Id != model.Id || model.Original is { } observed && observed.ApplicationId != model.ApplicationId) {
            return Result<Guid>.Failure(IdentityInvalid());
        }
        if (!Enum.IsDefined(model.InterviewType) || !Enum.IsDefined(model.Outcome)) {
            return Result<Guid>.Failure(Error.Validation("Choose supported interview values."));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scopes = new List<string> { InterviewScope(model.Id ?? Guid.NewGuid()), ApplicationScope(model.ApplicationId) };
        if (model.InterviewerPartyId is { } interviewerId) {
            scopes.Add(OpportunityPartyReferences.ScopeKey(interviewerId));
        }
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, scopes, cancellationToken);
        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == model.ApplicationId, cancellationToken);
        if (application is null) {
            return Result<Guid>.Failure(Error.Validation("The recruitment application was not found.", "crmhr.recruiting.interview.application-not-found"));
        }

        if (model.InterviewerPartyId.HasValue) {
            var interviewerError = await ValidatePeopleOnlyPartyAsync(
                dbContext,
                model.InterviewerPartyId.Value,
                "Interviewer must reference an existing person.",
                "crmhr.recruiting.interview.interviewer-invalid",
                cancellationToken);
            if (interviewerError is not null) {
                return Result<Guid>.Failure(interviewerError);
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<RecruitmentInterview>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && (entity is null || Interview(entity) != model.Original)) {
            return Result<Guid>.Failure(Conflict());
        }
        if (entity is null) {
            entity = new RecruitmentInterview {
                ApplicationId = model.ApplicationId
            };
            dbContext.Set<RecruitmentInterview>().Add(entity);
        }

        entity.ApplicationId = model.ApplicationId;
        entity.ScheduledAtUtc = UtcTime(model.ScheduledAtLocal.Value);
        entity.InterviewType = model.InterviewType;
        entity.InterviewerPartyId = model.InterviewerPartyId;
        entity.Outcome = model.Outcome;
        entity.Feedback = model.Feedback.Trim();
        entity.Recommendation = model.Recommendation.Trim();

        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        onCommitted(new(entity.Id, Accepted(entity)));
        changes.PublishRecruitmentRecords(model.ApplicationId);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentInterviewSaved",
                $"Saved {model.InterviewType} interview",
                entity.Outcome.ToString(),
                ArtifactKind: nameof(RecruitmentInterview),
                ArtifactId: entity.Id,
                Route: Route(model.ApplicationId)),
            cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }


    public async Task<Result> DeleteRecruitmentInterviewAsync(Guid id, CancellationToken cancellationToken = default,
        RecruitmentInterviewObservation? original = null, Action<RecruitmentDeleteReceipt>? onCommitted = null) {
        if (original is null) {
            return Result.Failure(OriginalRequired());
        }
        if (id != original.Id || original.ApplicationId == Guid.Empty) {
            return Result.Failure(IdentityInvalid());
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, [InterviewScope(id), ApplicationScope(original.ApplicationId)], cancellationToken);
            var entity = await dbContext.Set<RecruitmentInterview>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (entity is null || Interview(entity) != original) {
                return Result.Failure(Conflict());
            }
            dbContext.Set<RecruitmentInterview>().Remove(entity);
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            onCommitted?.Invoke(new(original.ApplicationId, id));
            changes.PublishRecruitmentRecords(original.ApplicationId);
            await activityStream.RecordAsync(new ActivityWriteRequest("CRM / HR", "RecruitmentInterviewDeleted", "Deleted recruitment interview",
                ArtifactKind: nameof(RecruitmentInterview), ArtifactId: id, Route: Route(original.ApplicationId)), cancellationToken);
            return Result.Success();
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result.Failure(Conflict());
        }
    }
}

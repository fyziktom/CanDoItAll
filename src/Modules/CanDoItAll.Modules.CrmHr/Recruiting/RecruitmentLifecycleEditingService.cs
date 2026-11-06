using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Modules.CrmHr.RecruitmentEditValues;
using static CanDoItAll.Modules.CrmHr.RecruitmentPartyReferences;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitmentLifecycleEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IActivityStream activityStream, ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction coordinatedTransaction, CrmHrChanges changes) {
    public async Task<Result<Guid>> SaveLifecycleTaskAsync(LifecycleTaskEditorModel model, CancellationToken cancellationToken = default, Action<RecruitmentSaveReceipt<LifecycleTaskEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SaveCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (ProjectWriteAdmissionRejectedException) when (!committed) {
            return Result<Guid>.Failure(Conflict());
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(Conflict());
        }
    }

    private async Task<Result<Guid>> SaveCoreAsync(
        LifecycleTaskEditorModel model,
        CancellationToken cancellationToken, Action<RecruitmentSaveReceipt<LifecycleTaskEditorModel>> onCommitted) {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a candidate first.", "crmhr.recruiting.task.party-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Title)) {
            return Result<Guid>.Failure(Error.Validation("Task title is required.", "crmhr.recruiting.task.title-required"));
        }

        if (!model.OwnerPartyId.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Task owner is required.", "crmhr.recruiting.task.owner-required"));
        }

        if (!model.DueDate.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Task due date is required.", "crmhr.recruiting.task.due-date-required"));
        }

        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(OriginalRequired());
        }
        if (model.Original?.Id != model.Id || model.Original is { } observed && observed.PartyId != model.PartyId) {
            return Result<Guid>.Failure(IdentityInvalid());
        }
        if (!Enum.IsDefined(model.TaskKind) || !Enum.IsDefined(model.Status)) {
            return Result<Guid>.Failure(Error.Validation("Choose supported lifecycle task values."));
        }
        if (model.RelatedProjectId != model.RelatedProjectAdmission?.ProjectId) {
            return Result<Guid>.Failure(Error.Validation("Choose the current project lifetime before linking this task.", RecruitmentEditingErrors.OriginalRequired));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scopes = new List<string> { TasksScope(model.PartyId), OpportunityPartyReferences.ScopeKey(model.PartyId), OpportunityPartyReferences.ScopeKey(model.OwnerPartyId.Value) };
        if (model.RelatedProjectId is { } projectId) {
            scopes.Add(ProjectMutationScopeKeys.ForProject(projectId));
        }
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, scopes, cancellationToken);
        using var ownerFrame = coordinatedTransaction.Enter(dbContext);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.PartyId, cancellationToken);
        if (!partyExists) {
            return Result<Guid>.Failure(Error.Validation("The selected candidate was not found.", "crmhr.recruiting.task.party-not-found"));
        }

        var ownerError = await ValidatePeopleOnlyPartyAsync(
            dbContext,
            model.OwnerPartyId.Value,
            "Task owner must reference an existing person.",
            "crmhr.recruiting.task.owner-invalid",
            cancellationToken);
        if (ownerError is not null) {
            return Result<Guid>.Failure(ownerError);
        }

        if (model.RelatedProjectAdmission is { } project) {
            await admissions.RequireForMutationAsync(project, cancellationToken);
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<OnboardingTask>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && (entity is null || Task(entity) != model.Original)) {
            return Result<Guid>.Failure(Conflict());
        }
        if (entity is null) {
            entity = new OnboardingTask {
                PartyId = model.PartyId
            };
            dbContext.Set<OnboardingTask>().Add(entity);
        }

        entity.PartyId = model.PartyId;
        entity.TaskKind = model.TaskKind;
        entity.Title = model.Title.Trim();
        entity.OwnerPartyId = model.OwnerPartyId;
        entity.DueDateUtc = UtcDate(model.DueDate);
        entity.Status = model.Status;
        entity.RelatedProjectId = model.RelatedProjectId;
        entity.Notes = model.Notes.Trim();

        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var accepted = Accepted(entity);
        accepted.RelatedProjectAdmission = model.RelatedProjectAdmission;
        onCommitted(new(entity.Id, accepted));
        changes.PublishRecruitmentTasks(model.PartyId);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentLifecycleTaskSaved",
                $"Saved {model.TaskKind} task {entity.Title}",
                entity.Status.ToString(),
                ProjectId: entity.RelatedProjectId,
                ArtifactKind: nameof(OnboardingTask),
                ArtifactId: entity.Id,
                Route: PartyRoute(model.PartyId)),
            cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }


    public async Task<Result> DeleteLifecycleTaskAsync(Guid id, CancellationToken cancellationToken = default,
        LifecycleTaskObservation? original = null, Action<RecruitmentDeleteReceipt>? onCommitted = null) {
        if (original is null) {
            return Result.Failure(OriginalRequired());
        }
        if (id != original.Id || original.PartyId == Guid.Empty) {
            return Result.Failure(IdentityInvalid());
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, original.RelatedProjectId is { } projectId ? [TasksScope(original.PartyId), ProjectMutationScopeKeys.ForProject(projectId)] : [TasksScope(original.PartyId)], cancellationToken);
            var entity = await dbContext.Set<OnboardingTask>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (entity is null || Task(entity) != original) {
                return Result.Failure(Conflict());
            }
            dbContext.Set<OnboardingTask>().Remove(entity);
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            onCommitted?.Invoke(new(original.PartyId, id));
            changes.PublishRecruitmentTasks(original.PartyId);
            await activityStream.RecordAsync(new ActivityWriteRequest("CRM / HR", "RecruitmentLifecycleTaskDeleted", "Deleted lifecycle task",
                ArtifactKind: nameof(OnboardingTask), ArtifactId: id, Route: PartyRoute(original.PartyId)), cancellationToken);
            return Result.Success();
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result.Failure(Conflict());
        }
    }
}

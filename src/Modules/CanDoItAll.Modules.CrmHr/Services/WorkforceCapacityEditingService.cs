using CanDoItAll.Modules.Projects;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class WorkforceCapacityEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory,
    ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction coordinatedTransaction, CrmHrChanges changes) {
    public async Task<Result<Guid>> SaveCapacityBlockAsync(CapacityBlockEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<CapacityBlockEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SaveCapacityBlockCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (ProjectWriteAdmissionRejectedException) when (!committed) {
            return Result<Guid>.Failure(Error.Failure("The selected project lifetime changed. Choose it again before saving capacity.", CrmHrEditingErrorCodes.CapacityConflict));
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.CapacityConflict));
        }
    }

    private async Task<Result<Guid>> SaveCapacityBlockCoreAsync(CapacityBlockEditorModel model, CancellationToken cancellationToken,
        Action<WorkforceSaveReceipt<CapacityBlockEditorModel>>? onCommitted) {
        if (model.PartyId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a party before saving a capacity block.", "crmhr.capacity.party-required"));
        }
        if (!model.StartDate.HasValue || !model.EndDate.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Capacity block start and end dates are required.", "crmhr.capacity.date-required"));
        }
        if (model.EndDate < model.StartDate) {
            return Result<Guid>.Failure(Error.Validation("Capacity block end date must be on or after the start date.", "crmhr.capacity.date-range-invalid"));
        }
        if (model.Percentage is <= 0m or > 100m || !Enum.IsDefined(model.BlockKind)) {
            return Result<Guid>.Failure(Error.Validation("Choose a supported block kind and a percentage above 0 and at most 100.", "crmhr.capacity.percentage-range"));
        }
        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(Error.Validation("The original capacity block is required for replacement.", CrmHrEditingErrorCodes.CapacityOriginalRequired));
        }
        if (model.Original?.Id != model.Id || model.Original is { } original && original.PartyId != model.PartyId) {
            return Result<Guid>.Failure(Error.Validation("The capacity original belongs to another worker or row.", CrmHrEditingErrorCodes.CapacityIdentityInvalid));
        }
        if (model.RelatedProjectId != model.RelatedProjectAdmission?.ProjectId) {
            return Result<Guid>.Failure(Error.Validation("Choose the current project lifetime before linking capacity.", CrmHrEditingErrorCodes.CapacityOriginalRequired));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectIds = new Guid?[] { model.RelatedProjectId, model.Original?.RelatedProjectId }.OfType<Guid>();
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            projectIds.Select(ProjectMutationScopeKeys.ForProject).Append(WorkforceEditValues.ScopeKey(model.PartyId))
                .Append(OpportunityPartyReferences.ScopeKey(model.PartyId)).ToArray(), cancellationToken);
        using var ownerFrame = coordinatedTransaction.Enter(dbContext);
        if (model.RelatedProjectAdmission is { } project) {
            await admissions.RequireForMutationAsync(project, cancellationToken);
        }
        if (!await dbContext.Set<Party>().AnyAsync(row => row.Id == model.PartyId, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("The selected party was not found.", "crmhr.capacity.party-not-found"));
        }
        var entity = model.Id is { } id ? await dbContext.Set<CapacityBlock>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken) : null;
        if (entity is not null && entity.PartyId != model.PartyId) {
            return Result<Guid>.Failure(Error.Validation("The capacity block belongs to another worker.", CrmHrEditingErrorCodes.CapacityIdentityInvalid));
        }
        if (model.Id.HasValue && (entity is null || WorkforceEditValues.Capacity(entity) != model.Original)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.CapacityConflict));
        }
        if (entity is null) {
            entity = new() { PartyId = model.PartyId };
            dbContext.Set<CapacityBlock>().Add(entity);
        }
        entity.BlockKind = model.BlockKind;
        entity.StartDateUtc = WorkforceEditValues.UtcDate(model.StartDate)!.Value;
        entity.EndDateUtc = WorkforceEditValues.UtcDate(model.EndDate)!.Value;
        entity.Percentage = model.Percentage;
        entity.RelatedProjectId = model.RelatedProjectId;
        entity.Notes = model.Notes.Trim();
        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var accepted = WorkforceEditValues.AcceptedCapacity(entity);
        accepted.RelatedProjectAdmission = model.RelatedProjectAdmission;
        onCommitted?.Invoke(new(entity.Id, accepted));
        changes.PublishWorkforceRecords(model.PartyId, model.Id.HasValue ? EditingChangeKind.EditableContentChanged : EditingChangeKind.Created);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result> DeleteCapacityBlockAsync(Guid capacityBlockId, CancellationToken cancellationToken = default,
        CapacityBlockObservation? original = null, Action<WorkforceDeleteReceipt>? onCommitted = null) {
        if (original is null) {
            return Result.Failure(Error.Validation("The observed capacity block is required for deletion.", CrmHrEditingErrorCodes.CapacityOriginalRequired));
        }
        if (capacityBlockId != original.Id || original.PartyId == Guid.Empty) {
            return Result.Failure(Error.Validation("The capacity original identifies another row.", CrmHrEditingErrorCodes.CapacityIdentityInvalid));
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var scopes = new List<string> { WorkforceEditValues.ScopeKey(original.PartyId), OpportunityPartyReferences.ScopeKey(original.PartyId) };
            if (original.RelatedProjectId is { } projectId) {
                scopes.Add(ProjectMutationScopeKeys.ForProject(projectId));
            }
            await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, scopes, cancellationToken);
            var entity = await dbContext.Set<CapacityBlock>().SingleOrDefaultAsync(row => row.Id == capacityBlockId, cancellationToken);
            if (entity is null || WorkforceEditValues.Capacity(entity) != original) {
                return Result.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.CapacityConflict));
            }
            dbContext.Set<CapacityBlock>().Remove(entity);
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            onCommitted?.Invoke(new(original.PartyId, original.Id));
            changes.PublishWorkforceRecords(original.PartyId, EditingChangeKind.Deleted);
            return Result.Success();
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.CapacityConflict));
        }
    }

}

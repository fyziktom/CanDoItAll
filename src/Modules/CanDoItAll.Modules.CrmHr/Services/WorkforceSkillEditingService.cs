using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class WorkforceSkillEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory, CrmHrChanges changes) {
    public async Task<Result<Guid>> SaveSkillDefinitionAsync(SkillDefinitionEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<SkillDefinitionEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SaveSkillDefinitionCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
        }
    }

    private async Task<Result<Guid>> SaveSkillDefinitionCoreAsync(SkillDefinitionEditorModel model, CancellationToken cancellationToken,
        Action<WorkforceSaveReceipt<SkillDefinitionEditorModel>>? onCommitted) {
        if (string.IsNullOrWhiteSpace(model.Name)) {
            return Result<Guid>.Failure(Error.Validation("Skill name is required.", "crmhr.skills.name-required"));
        }
        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(Error.Validation("The original skill definition is required for replacement.", CrmHrEditingErrorCodes.SkillOriginalRequired));
        }
        if (model.Original?.Id != model.Id) {
            return Result<Guid>.Failure(Error.Validation("The original skill definition identifies another row.", CrmHrEditingErrorCodes.SkillIdentityInvalid));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext, WorkforceEditValues.SkillCatalogScopeKey, cancellationToken);
        var entity = model.Id is { } id ? await dbContext.Set<SkillDefinition>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken) : null;
        if (model.Id.HasValue && (entity is null || WorkforceEditValues.SkillDefinition(entity) != model.Original)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
        }
        var name = model.Name.Trim();
        if (await dbContext.Set<SkillDefinition>().AnyAsync(row => row.Name == name && row.Id != model.Id, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("A skill with that name already exists. Read its current definition before updating it.", CrmHrEditingErrorCodes.SkillConflict));
        }
        if (entity is null) {
            entity = new();
            dbContext.Set<SkillDefinition>().Add(entity);
        }
        entity.Name = name;
        entity.Category = model.Category.Trim();
        entity.Description = model.Description.Trim();
        entity.IsActive = model.IsActive;
        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        onCommitted?.Invoke(new(entity.Id, new() {
            Id = entity.Id, Name = entity.Name, Category = entity.Category, Description = entity.Description,
            IsActive = entity.IsActive, Original = WorkforceEditValues.SkillDefinition(entity)
        }));
        changes.PublishSkillCatalog(entity.Id, model.Id.HasValue ? EditingChangeKind.EditableContentChanged : EditingChangeKind.Created);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<Guid>> SavePartySkillAsync(PartySkillEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<PartySkillEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SavePartySkillCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
        }
    }

    private async Task<Result<Guid>> SavePartySkillCoreAsync(PartySkillEditorModel model, CancellationToken cancellationToken,
        Action<WorkforceSaveReceipt<PartySkillEditorModel>>? onCommitted) {
        if (model.PartyId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a party before saving a skill.", "crmhr.skills.party-required"));
        }
        if (model.SkillId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a skill before saving.", "crmhr.skills.skill-required"));
        }
        if (!Enum.IsDefined(model.Proficiency) || model.YearsExperience < 0) {
            return Result<Guid>.Failure(Error.Validation("Choose a supported proficiency and nonnegative years of experience.", "crmhr.skills.proficiency-invalid"));
        }
        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(Error.Validation("The original party skill is required for replacement.", CrmHrEditingErrorCodes.SkillOriginalRequired));
        }
        if (model.Original?.Id != model.Id || model.Original is { } original && original.PartyId != model.PartyId) {
            return Result<Guid>.Failure(Error.Validation("The skill original belongs to another worker or row.", CrmHrEditingErrorCodes.SkillIdentityInvalid));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            [WorkforceEditValues.ScopeKey(model.PartyId), OpportunityPartyReferences.ScopeKey(model.PartyId), WorkforceEditValues.SkillCatalogScopeKey], cancellationToken);
        if (!await dbContext.Set<Party>().AnyAsync(row => row.Id == model.PartyId, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("The selected party was not found.", "crmhr.skills.party-not-found"));
        }
        if (!await dbContext.Set<SkillDefinition>().AnyAsync(row => row.Id == model.SkillId, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("The selected skill was not found.", "crmhr.skills.skill-not-found"));
        }
        var entity = model.Id is { } id ? await dbContext.Set<PartySkill>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken) : null;
        if (entity is not null && entity.PartyId != model.PartyId) {
            return Result<Guid>.Failure(Error.Validation("The skill belongs to another worker.", CrmHrEditingErrorCodes.SkillIdentityInvalid));
        }
        if (model.Id.HasValue && (entity is null || WorkforceEditValues.Skill(entity) != model.Original)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
        }
        if (await dbContext.Set<PartySkill>().AnyAsync(row => row.PartyId == model.PartyId && row.SkillId == model.SkillId && row.Id != model.Id, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("This worker already has that skill. Select its current row before replacing it.", CrmHrEditingErrorCodes.SkillConflict));
        }
        if (entity is null) {
            entity = new() { PartyId = model.PartyId };
            dbContext.Set<PartySkill>().Add(entity);
        }
        entity.SkillId = model.SkillId;
        entity.Proficiency = model.Proficiency;
        entity.YearsExperience = model.YearsExperience;
        entity.CertificationStatus = model.CertificationStatus.Trim();
        entity.LastValidatedAtUtc = WorkforceEditValues.UtcDate(model.LastValidatedOn);
        entity.Notes = model.Notes.Trim();
        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        onCommitted?.Invoke(new(entity.Id, WorkforceEditValues.AcceptedSkill(entity)));
        changes.PublishWorkforceRecords(model.PartyId, model.Id.HasValue ? EditingChangeKind.EditableContentChanged : EditingChangeKind.Created);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result> DeletePartySkillAsync(Guid partySkillId, CancellationToken cancellationToken = default,
        PartySkillObservation? original = null, Action<WorkforceDeleteReceipt>? onCommitted = null) {
        if (original is null) {
            return Result.Failure(Error.Validation("The observed party skill is required for deletion.", CrmHrEditingErrorCodes.SkillOriginalRequired));
        }
        if (partySkillId != original.Id || original.PartyId == Guid.Empty) {
            return Result.Failure(Error.Validation("The skill original identifies another row.", CrmHrEditingErrorCodes.SkillIdentityInvalid));
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
                [WorkforceEditValues.ScopeKey(original.PartyId), OpportunityPartyReferences.ScopeKey(original.PartyId)], cancellationToken);
            var entity = await dbContext.Set<PartySkill>().SingleOrDefaultAsync(row => row.Id == partySkillId, cancellationToken);
            if (entity is null || WorkforceEditValues.Skill(entity) != original) {
                return Result.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
            }
            dbContext.Set<PartySkill>().Remove(entity);
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            onCommitted?.Invoke(new(original.PartyId, original.Id));
            changes.PublishWorkforceRecords(original.PartyId, EditingChangeKind.Deleted);
            return Result.Success();
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.SkillConflict));
        }
    }

}

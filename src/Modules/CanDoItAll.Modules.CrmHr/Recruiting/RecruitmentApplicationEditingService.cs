using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Modules.CrmHr.RecruitmentEditValues;
using static CanDoItAll.Modules.CrmHr.RecruitmentPartyReferences;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitmentApplicationEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService, IClock clock, IActivityStream activityStream, RecruitmentSearchProjection search, CrmHrChanges changes) : IRecruitmentApplicationEditing {
    public async Task<RecruitmentApplicationRead?> ReadApplicationAsync(Guid applicationId, IReadOnlyList<Guid> referenceIds, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(referenceIds);
        if (referenceIds.Count > 16) {
            throw new ArgumentException("A recruitment comparison accepts at most sixteen reference identities.", nameof(referenceIds));
        }
        var ids = referenceIds.ToHashSet();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var snapshot = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken) : null;
        var application = await context.Set<RecruitmentApplication>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == applicationId, cancellationToken);
        if (application is null) {
            return null;
        }
        var model = Accepted(application);
        var candidate = await context.Set<Party>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == application.PartyId, cancellationToken);
        if (candidate is null) {
            return null;
        }
        model.CandidateName = candidate.DisplayName;
        model.CandidateSummary = candidate.Summary;
        var contacts = await context.Set<PartyContactPoint>().AsNoTracking()
            .Where(row => row.PartyId == candidate.Id && row.IsPublic).OrderByDescending(row => row.IsPrimary).ToListAsync(cancellationToken);
        model.CandidateEmail = contacts.FirstOrDefault(row => row.ContactType == PartyContactType.Email)?.Value ?? string.Empty;
        model.CandidatePhone = contacts.FirstOrDefault(row => row.ContactType == PartyContactType.Phone)?.Value ?? string.Empty;
        foreach (var id in new Guid?[] { application.PartyId, application.TargetUnitPartyId, application.RecruiterPartyId, application.HiringManagerPartyId }.OfType<Guid>()) {
            ids.Add(id);
        }
        var names = await context.Set<Party>().AsNoTracking().Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.DisplayName, cancellationToken);
        return new(model, names);
    }

    public async Task<Result<Guid>> SaveRecruitmentApplicationAsync(RecruitmentApplicationEditorModel model, CancellationToken cancellationToken = default, Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>>? onCommitted = null) {
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
        RecruitmentApplicationEditorModel model,
        CancellationToken cancellationToken, Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>> onCommitted) {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.DesiredRole)) {
            return Result<Guid>.Failure(Error.Validation("Desired role is required.", "crmhr.recruiting.role-required"));
        }

        if (model.Id.HasValue && model.Original is null) {
            return Result<Guid>.Failure(OriginalRequired());
        }
        if (model.Original?.Id != model.Id || model.Original is { } observed && observed.PartyId != model.PartyId) {
            return Result<Guid>.Failure(IdentityInvalid());
        }
        if (!Enum.IsDefined(model.Stage) || !Enum.IsDefined(model.Decision)) {
            return Result<Guid>.Failure(Error.Validation("Choose supported recruitment stage and decision values."));
        }
        var normalizedActor = Actor(model.LastChangedBy);
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var referenceIds = new Guid?[] { model.PartyId, model.TargetUnitPartyId, model.RecruiterPartyId, model.HiringManagerPartyId }.OfType<Guid>();
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            referenceIds.Select(OpportunityPartyReferences.ScopeKey).Append(ApplicationScope(model.Id ?? Guid.NewGuid())).ToArray(), cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<RecruitmentApplication>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && (entity is null || Application(entity) != model.Original)) {
            return Result<Guid>.Failure(Conflict());
        }
        PartyDirectoryService.PartySaveOperation? candidateCreation = null;
        var partyIdResult = await ResolveCandidatePartyIdAsync(dbContext, model, operation => candidateCreation = operation, cancellationToken);
        if (!partyIdResult.IsSuccess) {
            return Result<Guid>.Failure(partyIdResult.Errors.ToArray());
        }

        var partyId = partyIdResult.Value;
        var validationError = await ValidateApplicationPartiesAsync(dbContext, model, partyId, cancellationToken);
        if (validationError is not null) {
            return Result<Guid>.Failure(validationError);
        }

        var isNew = entity is null;
        var previousStage = entity?.Stage;

        if (entity is null) {
            entity = new RecruitmentApplication {
                PartyId = partyId,
                CreatedAtUtc = clock.GetUtcNow()
            };
            dbContext.Set<RecruitmentApplication>().Add(entity);
        }

        entity.PartyId = partyId;
        entity.TargetUnitPartyId = model.TargetUnitPartyId;
        entity.RecruiterPartyId = model.RecruiterPartyId;
        entity.HiringManagerPartyId = model.HiringManagerPartyId;
        entity.DesiredRole = model.DesiredRole.Trim();
        entity.Source = model.Source.Trim();
        entity.Stage = model.Stage;
        entity.AvailableFromUtc = UtcDate(model.AvailableFrom);
        entity.Decision = model.Decision;
        entity.Notes = model.Notes.Trim();
        entity.UpdatedAtUtc = CrmHrEditVersions.Next(entity.UpdatedAtUtc, clock.GetUtcNow());

        var candidateName = dbContext.Set<Party>().Local.SingleOrDefault(row => row.Id == partyId)?.DisplayName
            ?? await LoadPartyDisplayNameAsync(dbContext, partyId, cancellationToken);
        if (isNew) {
            AddAudit(
                dbContext,
                entity.Id,
                "RecruitmentCreated",
                $"Created recruitment pipeline for {candidateName}",
                StageDetail(null, entity.Stage, model.StageNotes),
                normalizedActor,
                clock.GetUtcNow());
        }
        else if (previousStage.HasValue && previousStage.Value != entity.Stage) {
            AddAudit(
                dbContext,
                entity.Id,
                "RecruitmentStageChanged",
                $"Moved {candidateName} to {entity.Stage}",
                StageDetail(previousStage.Value, entity.Stage, model.StageNotes),
                normalizedActor,
                clock.GetUtcNow());
        }

        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var accepted = Accepted(entity);
        accepted.CandidateName = candidateName;
        if (candidateCreation is not null) {
            var acceptedParty = PartyEditorContent.Accepted(dbContext, partyId);
            accepted.CandidateSummary = acceptedParty.Summary;
            accepted.CandidateEmail = acceptedParty.ContactPoints.FirstOrDefault(row => row.ContactType == PartyContactType.Email && row.IsPrimary)?.Value ?? string.Empty;
            accepted.CandidatePhone = acceptedParty.ContactPoints.FirstOrDefault(row => row.ContactType == PartyContactType.Phone && row.IsPrimary)?.Value ?? string.Empty;
        }
        onCommitted(new(entity.Id, accepted));
        changes.PublishRecruitment(entity.Id);
        if (candidateCreation is not null) {
            changes.PublishParty(partyId, EditingChangeKind.Created);
            await partyDirectoryService.PublishPartySavedAsync(candidateCreation, cancellationToken);
        }
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                isNew ? "RecruitmentCreated" : "RecruitmentUpdated",
                isNew ? $"Created candidate {candidateName}" : $"Updated candidate {candidateName}",
                $"{entity.DesiredRole} / {entity.Stage}",
                ArtifactKind: nameof(RecruitmentApplication),
                ArtifactId: entity.Id,
                Route: Route(entity.Id),
                Actor: normalizedActor),
            cancellationToken);
        await search.UpsertAsync(entity.Id, cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }


    private async Task<Result<Guid>> ResolveCandidatePartyIdAsync(
        CrmHrDbContext dbContext,
        RecruitmentApplicationEditorModel model,
        Action<PartyDirectoryService.PartySaveOperation> prepared,
        CancellationToken cancellationToken) {
        if (model.PartyId.HasValue) {
            var candidateError = await ValidateRecruitmentCandidatePartyAsync(
                dbContext,
                model.PartyId.Value,
                cancellationToken);
            return candidateError is null
                ? Result<Guid>.Success(model.PartyId.Value)
                : Result<Guid>.Failure(candidateError);
        }

        if (string.IsNullOrWhiteSpace(model.CandidateName)) {
            return Result<Guid>.Failure(Error.Validation("Choose an existing candidate or provide a new candidate name.", "crmhr.recruiting.candidate-required"));
        }

        var savePartyResult = await partyDirectoryService.SavePartyCoreAsync(dbContext, new PartyEditorModel {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Candidate,
            DisplayName = model.CandidateName.Trim(),
            Summary = model.CandidateSummary.Trim(),
            LastChangedBy = Actor(model.LastChangedBy),
            Roles =
            [
                new PartyRoleAssignmentEditorModel {
                    RoleKind = PartyRoleKind.Candidate,
                    Title = "Candidate",
                    IsPrimary = true
                }
            ],
            ContactPoints = BuildCandidateContacts(model)
        }, cancellationToken);

        if (savePartyResult.IsFailure || savePartyResult.Value is null) {
            return Result<Guid>.Failure(savePartyResult.Errors.ToArray());
        }
        prepared(savePartyResult.Value);
        return Result<Guid>.Success(savePartyResult.Value.PartyId);
    }


    private static List<PartyContactPointEditorModel> BuildCandidateContacts(RecruitmentApplicationEditorModel model) {
        var contacts = new List<PartyContactPointEditorModel>();
        if (!string.IsNullOrWhiteSpace(model.CandidateEmail)) {
            var email = model.CandidateEmail.Trim();
            contacts.Add(new PartyContactPointEditorModel {
                ContactType = PartyContactType.Email,
                Label = "Primary email",
                Value = email,
                NormalizedValue = email.ToLowerInvariant(),
                IsPrimary = true,
                IsPublic = true
            });
        }

        if (!string.IsNullOrWhiteSpace(model.CandidatePhone)) {
            contacts.Add(new PartyContactPointEditorModel {
                ContactType = PartyContactType.Phone,
                Label = "Primary phone",
                Value = model.CandidatePhone.Trim(),
                NormalizedValue = model.CandidatePhone.Trim(),
                IsPrimary = string.IsNullOrWhiteSpace(model.CandidateEmail),
                IsPublic = true
            });
        }

        return contacts;
    }


    private async Task<string> LoadPartyDisplayNameAsync(CrmHrDbContext dbContext, Guid partyId, CancellationToken cancellationToken) {
        return await dbContext.Set<Party>()
            .Where(item => item.Id == partyId)
            .Select(item => item.DisplayName)
            .SingleAsync(cancellationToken);
    }

}

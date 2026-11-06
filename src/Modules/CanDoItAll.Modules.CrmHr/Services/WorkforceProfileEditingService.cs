using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class WorkforceProfileEditingService(IDbContextFactory<CrmHrDbContext> dbContextFactory, IClock clock,
    IActivityStream activityStream, WorkforceSearchProjection search, CrmHrChanges changes) : IWorkforceProfileEditing {
    public async Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await WorkforceProfileQueries.ReadWorkspaceAsync(context, partyId, cancellationToken);
    }

    public async Task<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?> ReadWorkforceProfileEditorAsync(Guid partyId, CancellationToken cancellationToken) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await WorkforceProfileQueries.ReadCurrentAsync(context, partyId, cancellationToken);
    }

    public async Task<Result<Guid>> SaveWorkforceProfileAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var committed = false;
        try {
            return await SaveWorkforceProfileCoreAsync(model, cancellationToken, receipt => {
                committed = true;
                onCommitted?.Invoke(receipt);
            });
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.WorkforceConflict));
        }
    }

    private async Task<Result<Guid>> SaveWorkforceProfileCoreAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken, Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>? onCommitted) {
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var referenceIds = new Guid?[] { model.PartyId, model.ManagerPartyId, model.HomeUnitPartyId }.OfType<Guid>();
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            referenceIds.Select(OpportunityPartyReferences.ScopeKey).Append(WorkforceEditValues.ScopeKey(model.PartyId)).ToArray(), cancellationToken);
        var prepared = await PrepareAsync(dbContext, model, cancellationToken);
        if (prepared.IsFailure) {
            return Result<Guid>.Failure(prepared.Errors);
        }
        var (party, profile) = prepared.Value;
        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        onCommitted?.Invoke(new(profile.Id, WorkforceEditValues.AcceptedProfile(party, profile)));
        await PublishSavedAsync(party, profile, cancellationToken);
        return Result<Guid>.Success(profile.Id);
    }

    internal async Task<Result<(Party Party, WorkforceProfile Profile)>> PrepareAsync(CrmHrDbContext dbContext,
        WorkforceProfileEditorModel model, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("Choose a party before saving the workforce profile.", "crmhr.workforce.party-required"));
        }

        if (!Enum.IsDefined(model.WorkforceKind)) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("Choose a supported workforce kind.", "crmhr.workforce.kind-invalid"));
        }
        if (model.StartDate is { } start && model.EndDate is { } end && start > end) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("The profile end date cannot precede its start date.", "crmhr.workforce.dates-invalid"));
        }

        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == model.PartyId, cancellationToken);
        if (party is null) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Failure("The selected party could not be found.", "crmhr.workforce.party-not-found"));
        }

        if (model.ManagerPartyId == model.PartyId) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("A party cannot manage itself.", "crmhr.workforce.self-manager"));
        }

        if (model.HomeUnitPartyId == model.PartyId) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("A party cannot reference itself as its home unit.", "crmhr.workforce.self-home-unit"));
        }

        if (model.InternalCostRate is < 0m || model.ExternalBillingRate is < 0m) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation(
                "Workforce rates cannot be negative.",
                "crmhr.workforce.rate-negative"));
        }

        if (!Enum.IsDefined(model.RateUnit)) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation(
                "Choose a supported workforce rate unit.",
                "crmhr.workforce.rate-unit-invalid"));
        }

        var rateCurrencyCode = string.IsNullOrWhiteSpace(model.RateCurrencyCode)
            ? "USD"
            : model.RateCurrencyCode.Trim().ToUpperInvariant();
        if (rateCurrencyCode.Length != 3 || rateCurrencyCode.Any(character => character is < 'A' or > 'Z')) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation(
                "Rate currency must be a three-letter currency code.",
                "crmhr.workforce.rate-currency-invalid"));
        }

        if (party.PartyType == PartyType.Person && model.WorkforceKind == WorkforceKind.DeliveryUnit) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("People cannot be saved as delivery units.", "crmhr.workforce.person-delivery-unit"));
        }

        if ((party.PartyType == PartyType.Organization || party.PartyType == PartyType.OrganizationUnit) && model.WorkforceKind != WorkforceKind.DeliveryUnit) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("Organizations and organization units can only use the delivery-unit workforce kind.", "crmhr.workforce.organization-kind"));
        }

        if (model.ManagerPartyId is Guid managerPartyId) {
            var manager = await dbContext.Set<Party>()
                .Select(item => new {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == managerPartyId, cancellationToken);
            if (manager is null || manager.PartyType != PartyType.Person) {
                return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("Manager must reference an existing person.", "crmhr.workforce.manager-invalid"));
            }
        }

        if (model.HomeUnitPartyId is Guid homeUnitPartyId) {
            var homeUnit = await dbContext.Set<Party>()
                .Select(item => new {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == homeUnitPartyId, cancellationToken);
            if (homeUnit is null || (homeUnit.PartyType != PartyType.Organization && homeUnit.PartyType != PartyType.OrganizationUnit)) {
                return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("Home unit must reference an existing organization or organization unit.", "crmhr.workforce.home-unit-invalid"));
            }
        }

        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
        var current = WorkforceEditValues.Profile(party.Id, party.PartyType, profile);
        if (model.Original is { } original) {
            if (original.PartyId != model.PartyId || model.Id != original.ProfileId) {
                return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("The workforce original belongs to a different profile.", CrmHrEditingErrorCodes.WorkforceIdentityInvalid));
            }
            if (original != current) {
                return Result<(Party Party, WorkforceProfile Profile)>.Failure(WorkforceEditValues.Conflict(CrmHrEditingErrorCodes.WorkforceConflict));
            }
        } else if (model.Id.HasValue || profile is not null) {
            return Result<(Party Party, WorkforceProfile Profile)>.Failure(Error.Validation("The original saved workforce profile is required for replacement.", CrmHrEditingErrorCodes.WorkforceOriginalRequired));
        }
        if (profile is null) {
            profile = new WorkforceProfile {
                PartyId = model.PartyId
            };
            dbContext.Set<WorkforceProfile>().Add(profile);
        }

        profile.WorkforceKind = model.WorkforceKind;
        profile.EmployeeCode = model.EmployeeCode.Trim();
        profile.JobTitle = model.JobTitle.Trim();
        profile.Discipline = model.Discipline.Trim();
        profile.Seniority = model.Seniority.Trim();
        profile.HomeUnitPartyId = model.HomeUnitPartyId;
        profile.ManagerPartyId = model.ManagerPartyId;
        profile.StartDateUtc = WorkforceEditValues.UtcDate(model.StartDate);
        profile.EndDateUtc = WorkforceEditValues.UtcDate(model.EndDate);
        profile.Location = model.Location.Trim();
        profile.TimeZone = model.TimeZone.Trim();
        profile.InternalCostRate = model.InternalCostRate;
        profile.ExternalBillingRate = model.ExternalBillingRate;
        profile.RateUnit = model.RateUnit;
        profile.RateCurrencyCode = rateCurrencyCode;
        profile.CapacityHoursPerWeek = model.CapacityHoursPerWeek <= 0m ? 40m : model.CapacityHoursPerWeek;
        profile.Status = model.Status.Trim();
        profile.Notes = model.Notes.Trim();

        var workforceRole = WorkforceEditValues.Role(model.WorkforceKind);
        var currentRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == model.PartyId)
            .ToListAsync(cancellationToken);
        if (!currentRoles.Any(item => item.RoleKind == workforceRole)) {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment {
                PartyId = model.PartyId,
                RoleKind = workforceRole,
                Title = workforceRole.ToString(),
                IsPrimary = currentRoles.Count == 0
            });
        }

        party.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy.Trim();
        party.UpdatedAtUtc = CrmHrEditVersions.Next(party.UpdatedAtUtc, clock.GetUtcNow());
        CrmHrAuditWriter.AddEntry(
            dbContext,
            nameof(WorkforceProfile),
            party.Id,
            "WorkforceProfileSaved",
            $"Saved workforce profile for '{party.DisplayName}'.",
            new {
                profile.WorkforceKind,
                profile.JobTitle,
                profile.Discipline,
                profile.Seniority,
                profile.Status,
                profile.HomeUnitPartyId,
                profile.ManagerPartyId
            },
            party.LastChangedBy,
            party.IsSensitive,
            party.UpdatedAtUtc);
        return Result<(Party Party, WorkforceProfile Profile)>.Success((party, profile));
    }

    internal async Task PublishSavedAsync(Party party, WorkforceProfile profile, CancellationToken cancellationToken) {
        changes.PublishParty(party.Id);
        await search.UpsertAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "WorkforceProfileSaved",
                $"Saved workforce profile for {party.DisplayName}",
                $"{profile.WorkforceKind} / {profile.JobTitle} / {profile.Status}",
                ArtifactKind: nameof(WorkforceProfile),
                ArtifactId: party.Id,
                Route: $"/crm-hr/workforce?partyId={party.Id}",
                Actor: party.LastChangedBy),
            cancellationToken);
    }
}

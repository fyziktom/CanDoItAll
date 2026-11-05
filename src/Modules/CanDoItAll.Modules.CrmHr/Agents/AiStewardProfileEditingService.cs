using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiStewardProfileEditingService(IDbContextFactory<CrmHrDbContext> contexts, IClock clock,
    CrmHrChanges changes, IActivityStream activity, AiAgentSearchProjection search) : IAiStewardProfileEditing {
    private const string SaveAction = "AiAgentProfileSaved";

    public async Task<AiStewardProfileEditorModel?> ReadAsync(Guid partyId, CancellationToken cancellationToken = default) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        await using var snapshot = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken) : null;
        var type = await context.Set<Party>().AsNoTracking().Where(row => row.Id == partyId)
            .Select(row => (PartyType?)row.PartyType).SingleOrDefaultAsync(cancellationToken);
        if (type != PartyType.AiAgent) {
            return null;
        }
        var profile = await context.Set<AiAgentProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.PartyId == partyId, cancellationToken);
        return AiStewardEditValues.Editor(partyId, type.Value, profile);
    }

    public async Task<Result<Guid>> SaveAsync(AiStewardProfileEditorModel model, CancellationToken cancellationToken = default,
        Action<AiStewardProfileSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        var draft = model.Snapshot();
        if (draft.Notes is null) {
            return Result<Guid>.Failure(Error.Validation("Business notes must be supplied; an empty string is allowed."));
        }
        if (draft.Original is not { } original) {
            return Result<Guid>.Failure(Error.Validation("Read the business profile before replacing it.", AiStewardEditingErrors.OriginalRequired));
        }
        if (draft.PartyId == Guid.Empty || original.PartyId != draft.PartyId || original.ProfileId != draft.Id) {
            return Result<Guid>.Failure(Error.Validation("The business profile identity differs from its original.", AiStewardEditingErrors.IdentityInvalid));
        }
        if (!Enum.IsDefined(draft.ValidationStatus)) {
            return Result<Guid>.Failure(Error.Validation("Choose a supported validation status."));
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var context = await contexts.CreateDbContextAsync(cancellationToken);
            var references = new Guid?[] { draft.PartyId, draft.OwnerPartyId, original.OwnerPartyId }.OfType<Guid>().Distinct();
            await using var transaction = await SerializableMutationScope.BeginAsync(context,
                references.Select(OpportunityPartyReferences.ScopeKey).Append(AiStewardEditValues.ScopeKey(draft.PartyId)).ToArray(), cancellationToken);
            var party = await context.Set<Party>().SingleOrDefaultAsync(row => row.Id == draft.PartyId, cancellationToken);
            if (party?.PartyType != PartyType.AiAgent || original.PartyType != PartyType.AiAgent) {
                return Result<Guid>.Failure(Error.Validation("The selected AI party is no longer available for stewardship.", AiStewardEditingErrors.PartyTypeConflict));
            }
            var profile = await context.Set<AiAgentProfile>().SingleOrDefaultAsync(row => row.PartyId == party.Id, cancellationToken);
            if (AiStewardEditValues.Observe(party.Id, party.PartyType, profile) != original) {
                return Result<Guid>.Failure(AiStewardEditValues.Conflict());
            }
            Party? owner = null;
            if (draft.OwnerPartyId is { } ownerId) {
                owner = await context.Set<Party>().SingleOrDefaultAsync(row => row.Id == ownerId, cancellationToken);
                if (owner?.PartyType != PartyType.Person || owner.Id == party.Id) {
                    return Result<Guid>.Failure(Error.Validation("The steward must reference a current person.", AiStewardEditingErrors.OwnerInvalid));
                }
            }
            if (profile is null) {
                profile = new() { PartyId = party.Id };
                context.Add(profile);
            }
            profile.OwnerPartyId = draft.OwnerPartyId;
            profile.ValidationStatus = draft.ValidationStatus;
            profile.LastReviewedAtUtc = AiStewardEditValues.UtcDate(draft.LastReviewedOn);
            profile.Notes = draft.Notes.Trim();
            var actor = string.IsNullOrWhiteSpace(draft.LastChangedBy) ? "crm-hr-ui" : draft.LastChangedBy.Trim();
            var roleAdded = false;
            if (owner is not null) {
                var roles = await context.Set<PartyRoleAssignment>().Where(row => row.PartyId == owner.Id).ToListAsync(cancellationToken);
                if (!roles.Any(row => row.RoleKind == PartyRoleKind.AiSteward)) {
                    context.Add(new PartyRoleAssignment { PartyId = owner.Id, RoleKind = PartyRoleKind.AiSteward, Title = "AI steward", IsPrimary = roles.Count == 0 });
                    owner.UpdatedAtUtc = CrmHrEditVersions.Next(owner.UpdatedAtUtc, clock.GetUtcNow());
                    owner.LastChangedBy = actor;
                    roleAdded = true;
                }
            }
            party.LastChangedBy = actor;
            party.UpdatedAtUtc = CrmHrEditVersions.Next(party.UpdatedAtUtc, clock.GetUtcNow());
            CrmHrAuditWriter.AddEntry(context, nameof(AiAgentProfile), party.Id, SaveAction,
                $"Saved AI agent stewardship for '{party.DisplayName}'.",
                new { profile.OwnerPartyId, profile.ValidationStatus, profile.LastReviewedAtUtc }, actor, party.IsSensitive, party.UpdatedAtUtc);
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            var accepted = AiStewardEditValues.Editor(party.Id, party.PartyType, profile);
            accepted.LastChangedBy = actor;
            onCommitted?.Invoke(new(profile.Id, accepted));
            changes.PublishParty(party.Id);
            if (roleAdded) {
                changes.PublishParty(owner!.Id);
            }
            await search.UpsertAsync(party.Id, cancellationToken);
            await activity.RecordAsync(new("CRM / HR", SaveAction, $"Saved AI agent stewardship for {party.DisplayName}",
                profile.ValidationStatus.ToString(), ArtifactKind: nameof(AiAgentProfile), ArtifactId: party.Id,
                Route: $"/crm-hr/agents?partyId={party.Id:D}", Actor: actor), cancellationToken);
            return Result<Guid>.Success(profile.Id);
        } catch (Exception exception) when (!committed && AiStewardEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(AiStewardEditValues.Conflict());
        }
    }
}

using System.Collections.Immutable;
using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class PartyMergePreflight(IDbContextFactory<CrmHrDbContext> factory,
    CoordinatedDatabaseTransaction transactions, CrmHrChanges changes, IProjectWorkAssignmentCommands work) {
    public const int MaximumRows = 5_000;

    public async Task<Result<PartyMergePreviewModel>> ReadAsync(Guid retainedId, Guid mergedId,
        CancellationToken cancellationToken = default) {
        if (retainedId == Guid.Empty || mergedId == Guid.Empty || retainedId == mergedId) {
            return Result<PartyMergePreviewModel>.Failure(Error.Validation("Choose two different current parties.",
                CrmHrEditingErrorCodes.PartyMergeOriginalRequired));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var snapshot = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        using var owner = transactions.Enter(context);
        return await ReadCurrentAsync(context, retainedId, mergedId, cancellationToken);
    }

    internal async Task<Result<PartyMergePreviewModel>> ReadCurrentAsync(CrmHrDbContext context,
        Guid retainedId, Guid mergedId, CancellationToken cancellationToken) {
        var partyIds = new[] { retainedId, mergedId };
        var parties = await context.Set<Party>().AsNoTracking().Where(row => partyIds.Contains(row.Id))
            .Select(row => new PartyMergePartyModel(row.Id, row.DisplayName, row.PartyType, row.LifecycleStatus, row.IsSensitive))
            .ToArrayAsync(cancellationToken);
        if (parties.Length != 2) {
            return Result<PartyMergePreviewModel>.Failure(Error.Failure("One or both selected parties are no longer available.",
                CrmHrEditingErrorCodes.PartyMergeConflict));
        }
        var people = await context.Set<PartyOrganizationAffiliation>().AsNoTracking().Where(row =>
            partyIds.Contains(row.PersonPartyId) || partyIds.Contains(row.OrganizationPartyId) ||
            (row.OrganizationUnitPartyId.HasValue && partyIds.Contains(row.OrganizationUnitPartyId.Value)) ||
            (row.ManagerPartyId.HasValue && partyIds.Contains(row.ManagerPartyId.Value)))
            .Select(row => row.PersonPartyId).Distinct().Order().Take(MaximumRows + 1).ToArrayAsync(cancellationToken);
        var affiliations = await context.Set<PartyOrganizationAffiliation>().AsNoTracking()
            .Where(row => people.Contains(row.PersonPartyId)).Select(row => row.Id).Order()
            .Take(MaximumRows + 1).ToArrayAsync(cancellationToken);
        if (people.Length > MaximumRows || affiliations.Length > MaximumRows) {
            return TooLarge();
        }
        var rows = await ReadVersionsAsync(context, partyIds, affiliations, cancellationToken);
        if (rows.Length > MaximumRows) {
            return TooLarge();
        }
        var workRows = await work.ReadPartyMergeForMutationAsync(retainedId, mergedId, affiliations, MaximumRows, cancellationToken);
        if (workRows.Rows.Length > MaximumRows) {
            return TooLarge();
        }
        var projects = await context.Set<ProjectPartyAssignment>().AsNoTracking().Where(row =>
            partyIds.Contains(row.PartyId) || (row.PartyOrganizationAffiliationId.HasValue && affiliations.Contains(row.PartyOrganizationAffiliationId.Value)))
            .Select(row => row.ProjectId).Distinct().ToArrayAsync(cancellationToken);
        var connectionIds = rows.Where(row => row.Member == PartyMergeMember.AccountConnection).Select(row => row.Id).ToArray();
        var accountProjects = await context.Set<CrmAccountConnectionProjectLink>().AsNoTracking()
            .Where(row => connectionIds.Contains(row.AccountConnectionId)).Select(row => row.ProjectId).Distinct().ToArrayAsync(cancellationToken);
        var original = new PartyMergeObservation(changes.Scope.Id, changes.Scope.ActivationGeneration, retainedId, mergedId,
            rows.ToImmutableArray(), people.ToImmutableArray(), projects.Concat(accountProjects).Concat(workRows.Rows.Select(row => row.ProjectId)).Distinct().Order().ToImmutableArray(),
            affiliations.ToImmutableArray(), workRows);
        return Result<PartyMergePreviewModel>.Success(new(parties.Single(row => row.Id == retainedId),
            parties.Single(row => row.Id == mergedId), original));
    }

    private static Result<PartyMergePreviewModel> TooLarge() => Result<PartyMergePreviewModel>.Failure(Error.Validation(
        $"This merge affects more than {MaximumRows:N0} rows. Reduce its scope before preparing a new preview.",
        CrmHrEditingErrorCodes.PartyMergeScopeLimit));

    private static Task<PartyMergeRowVersion[]> ReadVersionsAsync(CrmHrDbContext context, Guid[] parties,
        Guid[] affiliations, CancellationToken cancellationToken) => context.Database.SqlQuery<PartyMergeRowVersion>($"""
        WITH p AS (SELECT unnest({parties}) AS id), a AS (SELECT unnest({affiliations}) AS id),
        interactions AS (
            SELECT "Id" FROM "CrmHr_Interactions" WHERE "NextActionOwnerPartyId" IN (SELECT id FROM p)
            UNION SELECT "InteractionId" FROM "CrmHr_InteractionParties" WHERE "PartyId" IN (SELECT id FROM p)
        ), opportunities AS (
            SELECT "Id" FROM "CrmHr_Opportunities" WHERE "AccountPartyId" IN (SELECT id FROM p)
                OR "OwnerPartyId" IN (SELECT id FROM p) OR "DeliveryUnitPartyId" IN (SELECT id FROM p)
            UNION SELECT "OpportunityId" FROM "CrmHr_OpportunityParties" WHERE "PartyId" IN (SELECT id FROM p)
        ), applications AS (
            SELECT "Id" FROM "CrmHr_RecruitmentApplications" WHERE "PartyId" IN (SELECT id FROM p)
                OR "TargetUnitPartyId" IN (SELECT id FROM p) OR "RecruiterPartyId" IN (SELECT id FROM p)
                OR "HiringManagerPartyId" IN (SELECT id FROM p)
        ), connections AS (
            SELECT "Id" FROM "CrmHr_AccountStakeholders" WHERE "AccountPartyId" IN (SELECT id FROM p)
                OR "RelatedPartyId" IN (SELECT id FROM p)
        ), versions AS (
            SELECT {(int)PartyMergeMember.Party} AS "Member", "Id", xmin::text::bigint AS "Version"
                FROM "CrmHr_Parties" WHERE "Id" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Role}, "Id", xmin::text::bigint
                FROM "CrmHr_PartyRoles" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Contact}, "Id", xmin::text::bigint
                FROM "CrmHr_PartyContactPoints" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Address}, "Id", xmin::text::bigint
                FROM "CrmHr_PartyAddresses" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.ConfidentialNote}, "Id", xmin::text::bigint
                FROM "CrmHr_ConfidentialNotes" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Relationship}, "Id", xmin::text::bigint
                FROM "CrmHr_PartyRelationships" WHERE "SourcePartyId" IN (SELECT id FROM p) OR "TargetPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Affiliation}, "Id", xmin::text::bigint
                FROM "CrmHr_PartyOrganizationAffiliations" WHERE "Id" IN (SELECT id FROM a)
            UNION ALL SELECT {(int)PartyMergeMember.WorkforceProfile}, "Id", xmin::text::bigint
                FROM "CrmHr_WorkforceProfiles" WHERE "PartyId" IN (SELECT id FROM p)
                    OR "HomeUnitPartyId" IN (SELECT id FROM p) OR "ManagerPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Skill}, "Id", xmin::text::bigint
                FROM "CrmHr_PartySkills" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.Capacity}, "Id", xmin::text::bigint
                FROM "CrmHr_CapacityBlocks" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.AiProfile}, "Id", xmin::text::bigint
                FROM "CrmHr_AiAgentProfiles" WHERE "PartyId" IN (SELECT id FROM p) OR "OwnerPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.AiBinding}, "Id", xmin::text::bigint
                FROM "CrmHr_AiResourceBindings" WHERE "PartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.AccountProfile}, "Id", xmin::text::bigint
                FROM "CrmHr_AccountProfiles" WHERE "AccountPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.AccountConnection}, "Id", xmin::text::bigint
                FROM "CrmHr_AccountStakeholders" WHERE "Id" IN (SELECT "Id" FROM connections)
            UNION ALL SELECT {(int)PartyMergeMember.AccountConnectionProject}, "Id", xmin::text::bigint
                FROM "CrmHr_AccountConnectionProjects" WHERE "AccountConnectionId" IN (SELECT "Id" FROM connections)
            UNION ALL SELECT {(int)PartyMergeMember.Interaction}, "Id", xmin::text::bigint
                FROM "CrmHr_Interactions" WHERE "Id" IN (SELECT "Id" FROM interactions)
            UNION ALL SELECT {(int)PartyMergeMember.InteractionParty}, "Id", xmin::text::bigint
                FROM "CrmHr_InteractionParties" WHERE "InteractionId" IN (SELECT "Id" FROM interactions)
            UNION ALL SELECT {(int)PartyMergeMember.Opportunity}, "Id", xmin::text::bigint
                FROM "CrmHr_Opportunities" WHERE "Id" IN (SELECT "Id" FROM opportunities)
            UNION ALL SELECT {(int)PartyMergeMember.OpportunityParty}, "Id", xmin::text::bigint
                FROM "CrmHr_OpportunityParties" WHERE "OpportunityId" IN (SELECT "Id" FROM opportunities)
            UNION ALL SELECT {(int)PartyMergeMember.RecruitmentApplication}, "Id", xmin::text::bigint
                FROM "CrmHr_RecruitmentApplications" WHERE "Id" IN (SELECT "Id" FROM applications)
            UNION ALL SELECT {(int)PartyMergeMember.RecruitmentInterview}, "Id", xmin::text::bigint
                FROM "CrmHr_RecruitmentInterviews" WHERE "InterviewerPartyId" IN (SELECT id FROM p)
                    OR "ApplicationId" IN (SELECT "Id" FROM applications)
            UNION ALL SELECT {(int)PartyMergeMember.OnboardingTask}, "Id", xmin::text::bigint
                FROM "CrmHr_OnboardingTasks" WHERE "PartyId" IN (SELECT id FROM p) OR "OwnerPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.StaffingRequest}, "Id", xmin::text::bigint
                FROM "CrmHr_StaffingRequests" WHERE "RequestedByPartyId" IN (SELECT id FROM p) OR "DeliveryUnitPartyId" IN (SELECT id FROM p)
            UNION ALL SELECT {(int)PartyMergeMember.ProjectAssignment}, "Id", xmin::text::bigint
                FROM "CrmHr_ProjectPartyAssignments" WHERE "PartyId" IN (SELECT id FROM p) OR "PartyOrganizationAffiliationId" IN (SELECT id FROM a)
        )
        SELECT * FROM versions ORDER BY "Member", "Id" LIMIT {MaximumRows + 1}
        """).ToArrayAsync(cancellationToken);
}

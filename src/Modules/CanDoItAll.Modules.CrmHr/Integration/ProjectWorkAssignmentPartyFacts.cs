using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class ProjectWorkAssignmentPartyFacts(
    DbContextOptions<CrmHrDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    ProjectPartyAffiliationContextService affiliations) : IProjectWorkAssignmentPartyFacts {
    public async Task<IReadOnlyDictionary<Guid, ProjectWorkAssignmentPartyFact>> ReadForMutationAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(partyIds);
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new CrmHrDbContext(configured), cancellationToken);
        var rows = await context.Set<Party>().AsNoTracking().Where(item => partyIds.Contains(item.Id))
            .Select(item => new { item.Id, item.PartyType, item.DisplayName }).ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.Id,
            item => new ProjectWorkAssignmentPartyFact(item.Id, Map(item.PartyType), item.DisplayName));
    }

    public async Task<Error?> ValidateAffiliationsForMutationAsync(
        IReadOnlyCollection<ProjectWorkAssignmentAffiliationRequirement> requirements,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(requirements);
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new CrmHrDbContext(configured), cancellationToken);
        return await affiliations.ValidateAsync(context,
            requirements.Select(item => new ProjectPartyAffiliationValidation(item.PartyId,
                item.AffiliationId, item.StartsAtUtc, item.EndsAtUtc)).ToArray(), cancellationToken);
    }

    public async Task<Guid?> FindParticipationProjectForMutationAsync(Guid assignmentId,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new CrmHrDbContext(configured), cancellationToken);
        return await context.Set<ProjectPartyAssignment>().Where(item => item.Id == assignmentId)
            .Select(item => (Guid?)item.ProjectId).SingleOrDefaultAsync(cancellationToken);
    }

    private static ProjectPartyType Map(PartyType type) => type switch {
        PartyType.Person => ProjectPartyType.Person,
        PartyType.Organization => ProjectPartyType.Organization,
        PartyType.OrganizationUnit => ProjectPartyType.OrganizationUnit,
        PartyType.AiAgent => ProjectPartyType.AiAgent,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported party type.")
    };
}

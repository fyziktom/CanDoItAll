using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal sealed class CrmHrProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.CrmHr;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(CapacityBlock),
        typeof(CrmAccountConnectionProjectLink),
        typeof(InteractionRecord),
        typeof(OnboardingTask),
        typeof(Opportunity),
        typeof(ProjectPartyAssignment),
        typeof(ProjectPartyAssignmentMoveReceipt),
        typeof(StaffingRequest)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<CrmAccountConnectionProjectLink>()
                .AsNoTracking()
                .AnyAsync(cancellationToken))
        {
            residues.Add(new("CRM account-to-project links"));
        }

        if (await dbContext.Set<CapacityBlock>()
                .AsNoTracking()
                .AnyAsync(item => item.RelatedProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("CRM capacity blocks linked to projects"));
        }

        if (await dbContext.Set<InteractionRecord>()
                .AsNoTracking()
                .AnyAsync(item => item.RelatedProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("CRM interactions linked to projects"));
        }

        if (await dbContext.Set<OnboardingTask>()
                .AsNoTracking()
                .AnyAsync(item => item.RelatedProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("CRM onboarding tasks linked to projects"));
        }

        if (await dbContext.Set<Opportunity>()
                .AsNoTracking()
                .AnyAsync(item => item.LinkedProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("CRM opportunities linked to projects"));
        }

        if (await dbContext.Set<ProjectPartyAssignmentMoveReceipt>()
                .AsNoTracking()
                .AnyAsync(cancellationToken))
        {
            residues.Add(new("CRM project-assignment move receipts"));
        }

        if (await dbContext.Set<ProjectPartyAssignment>()
                .AsNoTracking()
                .AnyAsync(cancellationToken))
        {
            residues.Add(new("CRM project party assignments"));
        }

        if (await dbContext.Set<StaffingRequest>()
                .AsNoTracking()
                .AnyAsync(item => item.ProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("CRM staffing requests linked to projects"));
        }

        return residues;
    }
}

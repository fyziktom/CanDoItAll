using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

internal sealed class PromptsProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Prompts;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(PromptArtifact),
        typeof(PromptUsageRecord)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<PromptArtifact>()
                .AsNoTracking()
                .AnyAsync(item => item.ProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("prompt artifacts linked to projects"));
        }

        if (await dbContext.Set<PromptUsageRecord>()
                .AsNoTracking()
                .AnyAsync(item => item.ProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("prompt usage linked to projects"));
        }

        return residues;
    }
}

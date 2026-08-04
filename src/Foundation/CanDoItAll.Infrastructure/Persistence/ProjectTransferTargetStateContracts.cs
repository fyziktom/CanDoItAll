namespace CanDoItAll.Infrastructure.Persistence;

public enum ProjectTransferTargetStateArea
{
    Infrastructure,
    AgentFramework,
    Collaboration,
    CrmHr,
    Processes,
    Projects,
    Prompts,
    Resources,
    SchedulerPlanner,
    TestLab,
    Workbench,
    Workspace
}

public sealed record ProjectTransferTargetStateResidue(string Description);

public interface IProjectTransferTargetStateParticipant
{
    ProjectTransferTargetStateArea Area { get; }

    IReadOnlyCollection<Type> EntityTypesToLock { get; }

    Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken);
}

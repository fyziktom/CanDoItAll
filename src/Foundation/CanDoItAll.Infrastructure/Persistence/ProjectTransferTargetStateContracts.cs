namespace CanDoItAll.Infrastructure.Persistence;

public enum ProjectTransferTargetStateArea {
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

public enum ProjectTransferTargetInspectionMode {
    Independent,
    Locked
}

public sealed record ProjectTransferTargetInspection(Guid Id, Guid TargetProfileId, ProjectTransferTargetInspectionMode Mode);

public interface IProjectTransferTargetStateParticipant {
    ProjectTransferTargetStateArea Area { get; }

    IReadOnlyCollection<Type> EntityTypesToLock { get; }

    Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request,
        CancellationToken cancellationToken);
}

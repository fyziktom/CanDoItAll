using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessStructureAssignmentFact(Guid RunId, string LaunchVariablesJson, DateTimeOffset CreatedAtUtc);
public sealed record ProcessStructureRuntimeFact(Guid RunId, Guid RootRunId, Guid? PlanId,
    ProcessRuntimeStatus Status, DateTimeOffset UpdatedAtUtc);
public sealed record ProcessStructurePlanFact(Guid PlanId, Guid DefinitionId, DateTimeOffset CreatedAtUtc);
public sealed record ProcessStructureStepStatistics(Guid RunId, int TotalStepCount, int CompletedStepCount,
    int BlockedStepCount, int WaitingApprovalStepCount, int ActiveStepCount);
public sealed record ProcessStructureRuntimeFacts(IReadOnlyList<ProcessStructureRuntimeFact> LinkedRuns,
    IReadOnlyList<ProcessStructureRuntimeFact> RootRuns, IReadOnlyList<ProcessStructurePlanFact> Plans,
    IReadOnlyList<ProcessStructureStepStatistics> Statistics);

public static class ProcessStructureProjectionQueryLimits {
    public const int MaximumRunIds = 4096;
    public const int MaximumAssignments = 4096;
}

public interface IProcessStructureProjectionQueryService : IProcessRunRecordReader {
    Task<ProcessRunRecordPage> ListForMutationAsync(ProcessRunRecordListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessStructureAssignmentFact>> GetProjectAssignmentsAsync(Guid projectId,
        IReadOnlyCollection<Guid> excludedRunIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessStructureAssignmentFact>> GetProjectAssignmentsForMutationAsync(Guid projectId,
        IReadOnlyCollection<Guid> excludedRunIds, CancellationToken cancellationToken = default);
    Task<ProcessStructureRuntimeFacts> GetRuntimeFactsAsync(IReadOnlyCollection<Guid> runIds, CancellationToken cancellationToken = default);
    Task<ProcessStructureRuntimeFacts> GetRuntimeFactsForMutationAsync(IReadOnlyCollection<Guid> runIds, CancellationToken cancellationToken = default);
}

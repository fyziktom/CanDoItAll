using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes;

public sealed record ProcessRunDetailToolData(
    ProcessRunListItem Run,
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> DecisionRecords,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessImprovementViewModel> Improvements);

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessLaunchPlanAccessSummary(
    Guid LaunchPlanId,
    Guid ProcessDefinitionId,
    Guid? ProjectId);

public sealed record ProcessStepRunAccessSummary(
    Guid StepRunId,
    Guid ProcessRunId,
    Guid ProcessDefinitionId,
    Guid? ProjectId);

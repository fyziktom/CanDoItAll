namespace CanDoItAll.Processes.Builder;

public sealed record ProcessBuildPlan(string PlanHash, IReadOnlyList<string> StepKeys);

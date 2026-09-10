namespace CanDoItAll.Modules.TestLab;

public sealed record TestPlanProjectionFact(
    Guid Id,
    string Title,
    string Phase,
    string CoverageGoal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TestPlanProjectionScopeFact(Guid? ProjectId);

using CanDoItAll.Application.UI.Dashboard;

namespace CanDoItAll.Application.UiSandbox;

public enum DashboardScenario {
    Loading,
    Empty,
    Populated,
    PartialPricing,
    Stale,
    Failure
}

public static class DashboardScenarios {
    public static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid WorkflowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid WorkflowRunId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ProcessRunId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static DashboardPresentation Create(DashboardScenario scenario, DashboardActivityTab activity = DashboardActivityTab.Workflows) {
        var data = new DashboardSnapshotData(
            scenario == DashboardScenario.Empty ? [] : [new(ProjectId, "Release planning", "Preparation",
                new("Active", DashboardStatusTone.Info), Now.AddMinutes(-5))],
            DashboardActivityMode.Active,
            scenario == DashboardScenario.Empty ? [] : [new(WorkflowId, WorkflowRunId, "Delivery workflow", "Preparing artifacts",
                new("Running", DashboardStatusTone.Info), Now.AddMinutes(-2))],
            DashboardActivityMode.RecentFallback,
            scenario == DashboardScenario.Empty ? [] : [new(ProcessRunId, "Review process", "Release planning",
                new("Completed", DashboardStatusTone.Success), Now.AddMinutes(-10), true)],
            new(scenario == DashboardScenario.Empty ? 0 : 12_345, scenario == DashboardScenario.Empty ? 0 : 0.12345m,
                scenario == DashboardScenario.PartialPricing ? 2 : 0, Now));
        DashboardSnapshotRead? read = scenario is DashboardScenario.Loading or DashboardScenario.Failure ? null
            : new(new(data, Now.AddMinutes(-1)), Now.AddMinutes(4),
                scenario == DashboardScenario.Stale ? DashboardSnapshotState.StaleAfterRefreshFailure : DashboardSnapshotState.Current,
                scenario == DashboardScenario.Stale ? Now : null);
        return new(read, Now, scenario == DashboardScenario.Loading ? null : Now.AddMinutes(4),
            scenario == DashboardScenario.Failure ? "The scenario owner could not read the snapshot." : null,
            scenario == DashboardScenario.Loading, false, activity);
    }
}

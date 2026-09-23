using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Diagnostics;

namespace CanDoItAll.AgentFramework.UiSandbox;

public static class DiagnosticsSandboxFixture {
    public static ImmutableArray<string> Scenarios { get; } = ["loading", "ready", "dashboard-stale", "agents-unavailable", "runs-stale", "partial", "empty", "adversarial"];
    public const string Forbidden = "diagnostics-private-sentinel";
    public static SandboxDashboardSnapshot Dashboard(bool poison = false) => new(4, 2, 3, 12, 0, 0, 1, 23,
        new("PolicyOnlyLocal", Forbidden + "/private", "https://user:password@example.invalid/?token=" + Forbidden,
            Forbidden, Forbidden, false, Forbidden));
    public static ExecutionRunRecord Run(bool poison = false) => new(Guid.Parse("74ebc360-280f-4eb9-a982-340000000001"), Guid.Empty, null,
        poison ? "<script id='diagnostics-injected'>alert(1)</script> " + new string('界', 300) : "Recent execution",
        "manual", "", "", "", "", "", Forbidden, Forbidden, Forbidden, "Fixture provider", "fixture-model",
        ExecutionState.Failed, RunOutcome.Failed, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, "", Forbidden, []);
    public static DiagnosticsPresentation Create(string? scenario) {
        var ready = new DiagnosticsReadState(DiagnosticsReadPhase.Ready, 1, 1);
        var stale = new DiagnosticsReadState(DiagnosticsReadPhase.Stale, 2, 1);
        var failed = new DiagnosticsReadState(DiagnosticsReadPhase.Failed, 1);
        var value = new DiagnosticsPresentation(DiagnosticsPresentationMapping.Dashboard(Dashboard()),
            [DiagnosticsPresentationMapping.Run(Run(scenario == "adversarial"))], ready, ready, ready);
        return scenario switch {
            "loading" => DiagnosticsPresentation.Initial,
            "dashboard-stale" => value with { DashboardState = stale },
            "agents-unavailable" => value with { AgentsState = failed },
            "runs-stale" => value with { RunsState = stale },
            "partial" => value with { Dashboard = null, DashboardState = failed, AgentsState = failed },
            "empty" => value with { Runs = [] },
            _ => value
        };
    }
}

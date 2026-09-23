using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Governance;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum GovernanceSandboxScenario {
    Loading, AllAgents, SelectedAgent, MissingAgent, EmptyRuns, DetailLoading, DetailFailure,
    StaleRefresh, RunTransition, RemovedRun, ApprovalsArtifacts, LongText, UtcTimestamp, DeniedPayloads
}

public sealed record GovernanceScenarioDefinition(GovernanceSandboxScenario Scenario, string Token, string Label);

public sealed class GovernanceSandboxFixture {
    public static readonly Guid AgentA = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000001");
    public static readonly Guid AgentB = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000002");
    public static readonly Guid Run1 = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000011");
    public static readonly Guid Run2 = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000012");
    private static readonly Guid missing = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000099");
    private static readonly ImmutableArray<GovernanceAgentOption> agents = [new(AgentA, "Governance agent A"), new(AgentB, "Governance agent B")];
    public static ImmutableArray<GovernanceScenarioDefinition> Scenarios { get; } = [
        new(GovernanceSandboxScenario.Loading, "loading", "Loading"),
        new(GovernanceSandboxScenario.AllAgents, "all-agents", "All agents"),
        new(GovernanceSandboxScenario.SelectedAgent, "selected-agent", "Selected agent"),
        new(GovernanceSandboxScenario.MissingAgent, "missing-agent", "Missing explicit agent"),
        new(GovernanceSandboxScenario.EmptyRuns, "empty-runs", "Empty runs"),
        new(GovernanceSandboxScenario.DetailLoading, "detail-loading", "List ready while detail loads"),
        new(GovernanceSandboxScenario.DetailFailure, "detail-failure", "Detail failure with retained list"),
        new(GovernanceSandboxScenario.StaleRefresh, "stale-refresh", "Stale refresh"),
        new(GovernanceSandboxScenario.RunTransition, "run-transition", "R1 to R2 transition"),
        new(GovernanceSandboxScenario.RemovedRun, "removed-run", "Selected run removed"),
        new(GovernanceSandboxScenario.ApprovalsArtifacts, "approvals-artifacts", "Approvals and artifacts"),
        new(GovernanceSandboxScenario.LongText, "long-text", "Long and adversarial text"),
        new(GovernanceSandboxScenario.UtcTimestamp, "utc", "Deterministic UTC"),
        new(GovernanceSandboxScenario.DeniedPayloads, "denied-payloads", "Allowlisted fields only")
    ];

    public GovernancePresentation Presentation { get; private set; } = GovernancePresentation.Empty;
    public GovernanceViewState State { get; private set; } = GovernanceViewState.Initial;
    public GovernanceSandboxScenario Scenario { get; private set; }
    public string IntentLog { get; private set; } = "No intent yet.";

    public GovernanceSandboxFixture() => SetScenario(GovernanceSandboxScenario.SelectedAgent);

    public static GovernanceSandboxScenario Parse(string? token)
        => Scenarios.FirstOrDefault(item => string.Equals(item.Token, token, StringComparison.OrdinalIgnoreCase))?.Scenario
            ?? GovernanceSandboxScenario.SelectedAgent;

    public void SetScenario(GovernanceSandboxScenario scenario) {
        Scenario = scenario;
        IntentLog = "No intent yet.";
        SelectAgent(scenario == GovernanceSandboxScenario.AllAgents ? null : AgentA);
        switch (scenario) {
            case GovernanceSandboxScenario.Loading:
                Presentation = GovernancePresentation.Empty;
                State = GovernanceViewState.Initial;
                break;
            case GovernanceSandboxScenario.MissingAgent:
                Presentation = new(agents, [], null);
                State = State with { DesiredAgentId = missing, AcceptedAgentId = null, AgentResolved = false,
                    DesiredRunId = null, AcceptedRunId = null,
                    Catalog = new(GovernanceReadPhase.Unavailable, "The requested technical agent is unavailable. Retry its catalog lookup."),
                    List = GovernanceLaneState.Idle, Detail = GovernanceLaneState.Idle };
                break;
            case GovernanceSandboxScenario.EmptyRuns:
                Presentation = new(agents, [], null);
                State = State with { DesiredRunId = null, AcceptedRunId = null, Detail = GovernanceLaneState.Idle };
                break;
            case GovernanceSandboxScenario.DetailLoading:
            case GovernanceSandboxScenario.RunTransition:
                Presentation = Presentation with { Detail = null };
                State = State with { DesiredRunId = scenario == GovernanceSandboxScenario.RunTransition ? Run2 : Run1,
                    AcceptedRunId = null, Detail = new(GovernanceReadPhase.Loading) };
                break;
            case GovernanceSandboxScenario.DetailFailure:
                Presentation = Presentation with { Detail = null };
                State = State with { AcceptedRunId = null,
                    Detail = new(GovernanceReadPhase.Failed, "Execution details could not be loaded. Retry this run.") };
                break;
            case GovernanceSandboxScenario.StaleRefresh:
                State = State with { List = new(GovernanceReadPhase.Stale, "Execution runs could not be refreshed. Retry the run list.") };
                break;
            case GovernanceSandboxScenario.RemovedRun:
                Presentation = Presentation with { Runs = [Presentation.Runs[0]], Detail = null };
                State = State with { DesiredRunId = Run2, AcceptedRunId = null,
                    Detail = new(GovernanceReadPhase.Unavailable, "The selected execution run is no longer in this result. Choose another run or refresh.") };
                break;
            case GovernanceSandboxScenario.DeniedPayloads:
                var poison = GovernancePoisonFixture.Create();
                var projected = GovernancePresentationMapping.Detail(poison, "Governance agent A");
                Presentation = new(agents, [projected.Run], projected);
                break;
            case GovernanceSandboxScenario.LongText:
                var run = Presentation.Runs[0] with {
                    Title = GovernancePresentationMapping.Text("<img src=x onerror=alert(1)> " + new string('W', 2000)),
                    Provider = GovernancePresentationMapping.Text(new string('P', 1500))
                };
                Presentation = Presentation with { Runs = [run, Presentation.Runs[1]], Detail = Detail(run) };
                break;
        }
    }

    public void Apply(GovernanceIntent intent) {
        switch (intent) {
            case GovernanceIntent.SelectAgent select:
                SelectAgent(select.AgentId);
                IntentLog = $"SelectAgent: {select.AgentId?.ToString() ?? "All agents"}. Sample state only.";
                break;
            case GovernanceIntent.SelectRun select:
                SelectRun(select.RunId);
                IntentLog = $"SelectRun: {select.RunId}. Sample state only.";
                break;
            case GovernanceIntent.RetryDetail:
                if (State.DesiredRunId is { } runId) {
                    SelectRun(runId);
                }
                IntentLog = "RetryDetail: sample read completed.";
                break;
            case GovernanceIntent.RetryCatalog:
                if (State.DesiredAgentId == missing) {
                    IntentLog = "RetryCatalog: explicit sample target remains unavailable.";
                    break;
                }
                SelectAgent(State.DesiredAgentId);
                IntentLog = "RetryCatalog: sample read completed.";
                break;
            case GovernanceIntent.RetryList:
            case GovernanceIntent.Refresh:
                var desired = State.DesiredRunId;
                SelectAgent(State.DesiredAgentId);
                if (desired.HasValue) {
                    SelectRun(desired.Value);
                }
                IntentLog = intent is GovernanceIntent.Refresh ? "Refresh: sample reads completed." : "RetryList: sample read completed.";
                break;
        }
    }

    private void SelectAgent(Guid? agentId) {
        if (agentId == Guid.Empty || agentId.HasValue && agents.All(agent => agent.Id != agentId)) {
            return;
        }
        ImmutableArray<GovernanceRunPresentation> runs = [Run(Run1, AgentA, "Governance run 1"),
            Run(Run2, AgentA, "Governance run 2"),
            Run(Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000021"), AgentB, "Governance run B")];
        if (agentId.HasValue) {
            runs = runs.Where(run => run.AgentId == agentId).ToImmutableArray();
        }
        Presentation = new(agents, runs, Detail(runs[0]));
        State = new(agentId, agentId, true, runs[0].Id, runs[0].Id,
            GovernanceLaneState.Ready, GovernanceLaneState.Ready, GovernanceLaneState.Ready);
    }

    private void SelectRun(Guid runId) {
        var run = Presentation.Runs.FirstOrDefault(item => item.Id == runId);
        if (run is null) {
            return;
        }
        Presentation = Presentation with { Detail = Detail(run) };
        State = State with { DesiredRunId = runId, AcceptedRunId = runId, Detail = GovernanceLaneState.Ready };
    }

    private static GovernanceRunPresentation Run(Guid id, Guid agent, string title)
        => new(id, agent, title, agents.Single(item => item.Id == agent).Label, "Completed", "Succeeded",
            GovernanceTone.Success, GovernancePresentationMapping.Timestamp(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero)),
            "Sample provider", "sample-model", "Completed successfully.", "manual", "", "", "", false);

    private static GovernanceDetailPresentation Detail(GovernanceRunPresentation run) {
        var observed = run.Updated;
        return new(run, 2, 1, 1, 1,
            [new("Review output", "Approval", observed, "Approval is pending.", "Pending", GovernanceTone.Warning),
             new("Read artifact", "Approval", observed, "Approval was granted.", "Approved", GovernanceTone.Success)],
            [new("Report", "Artifact", observed, "artifacts/report.txt", "text/plain")],
            [new("Saved checkpoint", "Checkpoint", observed, "Execution state was saved.", "Completed")],
            [new("Tool completed", "Receipt", observed, "A tool receipt was recorded.", "Succeeded", GovernanceTone.Success)],
            [new("Execution", "Completed", observed, "Execution state recorded.", "Completed", GovernanceTone.Success)],
            new(new(1, 1, 0, 3, 4, 1), [new("Sample provider", "sample-model", observed,
                "Input 3, output 4, tools 1", "Succeeded", GovernanceTone.Success)]));
    }
}

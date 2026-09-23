using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.AgentFramework.UI.Governance.GovernancePresentationMapping;

namespace CanDoItAll.AgentFramework.UI.Diagnostics;

public enum DiagnosticsLane { Dashboard, Agents, Runs }
public enum DiagnosticsReadPhase { Loading, Refreshing, Ready, Stale, Failed }
public sealed record DiagnosticsReadState(DiagnosticsReadPhase Phase, long DesiredRevision = 0, long? AcceptedRevision = null) {
    public bool IsBusy => Phase is DiagnosticsReadPhase.Loading or DiagnosticsReadPhase.Refreshing;
    public string? Error => Phase switch {
        DiagnosticsReadPhase.Stale => "Refresh failed. Previously accepted data is stale; retry to update it.",
        DiagnosticsReadPhase.Failed => "This diagnostics section is unavailable. Retry to load it.",
        _ => null
    };
}
public sealed record DiagnosticsBoundary(string Host, string Mode, string Filesystem, string Network, string Credentials, string Notes);
public sealed record DiagnosticsDashboard(int Agents, int Templates, int Providers, int Capabilities, int ActiveRuns, int FailedRuns, DiagnosticsBoundary Boundary);
public sealed record DiagnosticsRun(Guid Id, Guid AgentId, string Title, string Agent, string Provider, string Model,
    string State, string? Outcome, string Tone, string Updated, bool IsFailure);
public sealed record DiagnosticsPresentation(DiagnosticsDashboard? Dashboard, ImmutableArray<DiagnosticsRun> Runs,
    DiagnosticsReadState DashboardState, DiagnosticsReadState AgentsState, DiagnosticsReadState RunsState) {
    public static DiagnosticsPresentation Initial { get; } = new(null, [], new(DiagnosticsReadPhase.Loading), new(DiagnosticsReadPhase.Loading), new(DiagnosticsReadPhase.Loading));
    public bool IsBusy => DashboardState.IsBusy || AgentsState.IsBusy || RunsState.IsBusy;
    public ImmutableArray<DiagnosticsRun> Failures => Runs.Where(run => run.IsFailure).ToImmutableArray();
    public DiagnosticsReadState ReadState(DiagnosticsLane lane) => lane switch {
        DiagnosticsLane.Dashboard => DashboardState,
        DiagnosticsLane.Agents => AgentsState,
        _ => RunsState
    };
}
public abstract record DiagnosticsIntent {
    public sealed record Refresh : DiagnosticsIntent;
    public sealed record Retry(DiagnosticsLane Lane) : DiagnosticsIntent;
}

public static class DiagnosticsPresentationMapping {
    public static DiagnosticsDashboard Dashboard(SandboxDashboardSnapshot value) => new(
        Math.Max(0, value.AgentCount), Math.Max(0, value.TemplateCount), Math.Max(0, value.ProviderCount),
        Math.Max(0, value.CapabilityCount), Math.Max(0, value.ActiveRuns), Math.Max(0, value.FailedRuns), Boundary(value.ToolExecutionBoundary));

    public static DiagnosticsBoundary Boundary(ExecutionBoundaryDescriptor value) => value.Mode switch {
        "PolicyOnlyLocal" => new("Local best-effort process host", "Policy only; no host isolation",
            "Workspace-relative request shaping; processes inherit host filesystem rights.",
            "No host-enforced network boundary.", "Scrubbed environment allowlist; no container credential boundary.",
            "Policy checks, timeouts, output limits and process-tree cancellation do not provide OS or container isolation."),
        "Unknown" => new("Unconfigured workspace process host", "Unknown", "Not reported.", "Not reported.", "Not reported.",
            "No workspace process host has been registered for this runtime."),
        _ => new("Unrecognized workspace process host", "Unverified", "Not verified.", "Not verified.", "Not verified.",
            "This host has no reviewed display policy. Raw host details are omitted.")
    };

    public static DiagnosticsRun Run(ExecutionRunRecord value) => new(value.Id, value.AgentId, Text(value.Title), "Unknown agent",
        Text(value.ProviderName), Text(value.Model), value.State.ToString(), value.Outcome?.ToString(),
        value.Outcome == RunOutcome.Succeeded || value.State == ExecutionState.Completed ? "success"
            : value.Outcome == RunOutcome.Failed || value.State == ExecutionState.Failed ? "danger"
            : value.State == ExecutionState.WaitingOnTool ? "warning" : "info", Timestamp(value.UpdatedAtUtc),
        value.Outcome == RunOutcome.Failed || value.State == ExecutionState.Failed);
}

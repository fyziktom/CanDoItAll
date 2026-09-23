using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Modules.AgentFramework;

internal enum DiagnosticsBrowserMode { Normal, DashboardFailure, AgentsFailure, RunsFailure, HoldRuns, Poison }
internal sealed class DiagnosticsBrowserReads(IAgentDiagnosticsReads canonical, GovernanceBrowserState fixture) : IAgentDiagnosticsReads {
    public async Task<SandboxDashboardSnapshot> ReadDashboardAsync(CancellationToken token) {
        Interlocked.Increment(ref fixture.DiagnosticsDashboardReads);
        var value = await canonical.ReadDashboardAsync(token);
        if (fixture.DiagnosticsMode == DiagnosticsBrowserMode.DashboardFailure) {
            throw new InvalidOperationException(DiagnosticsSandboxFixture.Forbidden);
        }
        return fixture.DiagnosticsMode == DiagnosticsBrowserMode.Poison ? DiagnosticsSandboxFixture.Dashboard(true) : value;
    }
    public async Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken token) {
        var value = await canonical.ReadAgentsAsync(token);
        if (fixture.DiagnosticsMode == DiagnosticsBrowserMode.AgentsFailure) {
            throw new InvalidOperationException(DiagnosticsSandboxFixture.Forbidden);
        }
        return value;
    }
    public async Task<IReadOnlyList<ExecutionRunRecord>> ReadRecentRunsAsync(CancellationToken token) {
        var mode = fixture.DiagnosticsMode;
        var value = await canonical.ReadRecentRunsAsync(token);
        if (mode == DiagnosticsBrowserMode.RunsFailure) {
            throw new InvalidOperationException(DiagnosticsSandboxFixture.Forbidden);
        }
        if (mode == DiagnosticsBrowserMode.HoldRuns) {
            await fixture.HoldAsync(token);
        }
        return mode == DiagnosticsBrowserMode.Poison ? [DiagnosticsSandboxFixture.Run(true)] : value;
    }
}

using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentDiagnosticsReadsTests {
    [Fact]
    public async Task Registered_reads_use_canonical_counts_labels_and_twelve_run_window_concurrently() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: false, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = Assert.IsType<AgentDiagnosticsReads>(scope.ServiceProvider.GetRequiredService<IAgentDiagnosticsReads>());
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        for (var index = 0; index < 14; index++) {
            var at = DateTimeOffset.UnixEpoch.AddMinutes(index);
            var run = new ExecutionRunRecord(Guid.NewGuid(), agent.Id, null, "Diagnostics fixture", "manual", "", "", "", "", "",
                "{}", "Private input", "Private result", "Fixture", "model", ExecutionState.Failed, RunOutcome.Failed, at, at, null, null, "", null, []);
            await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        }
        var dashboard = reads.ReadDashboardAsync(CancellationToken.None);
        var agents = reads.ReadAgentsAsync(CancellationToken.None);
        var runs = reads.ReadRecentRunsAsync(CancellationToken.None);
        await Task.WhenAll(dashboard, agents, runs);
        Assert.Equal(12, runs.Result.Count);
        Assert.True(dashboard.Result.FailedRuns >= 14);
        Assert.Contains(agents.Result, item => item.Id == agent.Id);
        Assert.DoesNotContain(agents.Result, item => item.IsTemplate);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reads.ReadRecentRunsAsync(canceled.Token));
    }
}

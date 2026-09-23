using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentGovernanceReadsTests {
    [Fact]
    public async Task Registered_reads_filter_agent_and_preserve_canonical_order_and_take() {
        await using var host = await CreateHostAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = Assert.IsType<AgentGovernanceReads>(scope.ServiceProvider.GetRequiredService<IAgentGovernanceReads>());
        var catalog = await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync();
        var agent = catalog.Agents.First();
        var other = catalog.Agents.First(item => item.Id != agent.Id);
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var expected = new List<ExecutionRunRecord>();
        for (var index = 0; index < 33; index++) {
            var run = Run(agent.Id, index);
            expected.Add((await store.SaveExecutionRunDetailAsync(new(run, null, [], []))).Run);
        }
        var unrelated = await store.SaveExecutionRunDetailAsync(new(Run(other.Id, 40), null, [], []));
        var filtered = await reads.ReadRunsAsync(agent.Id, CancellationToken.None);
        Assert.Equal(expected.OrderByDescending(run => run.UpdatedAtUtc).Take(30).Select(run => run.Id), filtered.Select(run => run.Id));
        Assert.All(filtered, run => Assert.Equal(agent.Id, run.AgentId));
        Assert.Contains(await reads.ReadRunsAsync(null, CancellationToken.None), run => run.Id == unrelated.Run.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => reads.ReadRunsAsync(Guid.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task Registered_detail_reads_preserve_run_identity_and_governance_sections() {
        await using var host = await CreateHostAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var agent = (await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync()).Agents.First();
        var run = Run(agent.Id, 0);
        var detail = new ExecutionRunDetail(run, null,
            [new(Guid.NewGuid(), agent.Id, null, DateTimeOffset.UnixEpoch, ExecutionState.Completed, "Execution", "Private execution content") { ExecutionRunId = run.Id }],
            [new(Guid.NewGuid(), agent.Id, null, DateTimeOffset.UnixEpoch, RunOutcome.Succeeded, "Fixture", "model", 100, 3, 4, 1) { ExecutionRunId = run.Id }]) {
            Approvals = [new("approval", run.Id, "call", "Tool", "tool", "Private approval details", "{}", ExecutionApprovalStatus.Approved,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "manual", "", "")],
            Artifacts = [new(Guid.NewGuid(), run.Id, "text", "Result", "artifacts/result.txt", "text/plain", "Fixture", "Private summary", DateTimeOffset.UnixEpoch)],
            Checkpoints = [new(Guid.NewGuid(), run.Id, "session", "checkpoint", "Saved", ExecutionState.Completed, [],
                DateTimeOffset.UnixEpoch, null, "", "manual", "", "", "", "", "", "", "")]
        };
        await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().SaveExecutionRunDetailAsync(detail);
        var read = await scope.ServiceProvider.GetRequiredService<IAgentGovernanceReads>().ReadDetailAsync(run.Id, CancellationToken.None);
        Assert.Equal(run.Id, read.Run.Id);
        Assert.Equal(agent.Id, read.Run.AgentId);
        Assert.Single(read.ExecutionLog);
        Assert.Single(read.Metrics);
        Assert.Single(read.Approvals);
        Assert.Single(read.Artifacts);
        Assert.Single(read.Checkpoints);
    }

    [Fact]
    public async Task Registered_read_cancellation_propagates_without_execution_effects() {
        await using var host = await CreateHostAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IAgentGovernanceReads>();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = await store.ListExecutionRunsAsync();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reads.ReadAgentsAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reads.ReadRunsAsync(null, canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reads.ReadDetailAsync(Guid.NewGuid(), canceled.Token));
        Assert.Equal(before.Select(run => run.Id), (await store.ListExecutionRunsAsync()).Select(run => run.Id));
    }

    [Fact]
    public async Task Missing_canonical_run_remains_unavailable_without_fallback_selection() {
        await using var host = await CreateHostAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IAgentGovernanceReads>();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var missing = Guid.NewGuid();
        Assert.Null(await store.GetExecutionRunDetailAsync(missing));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reads.ReadDetailAsync(missing, CancellationToken.None));
        using var session = new AgentGovernanceSession(reads, NullLogger<AgentGovernanceSession>.Instance);
        await session.SetAgentAsync(null);
        var accepted = session.AcceptedRunId;
        await session.SelectRunAsync(missing);
        Assert.Equal(accepted, session.AcceptedRunId);
        Assert.DoesNotContain(session.Runs, run => run.Id == missing);
    }

    private static Task<ApiTestHost> CreateHostAsync()
        => ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: false, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
        });

    private static ExecutionRunRecord Run(Guid agentId, int index) => new(Guid.NewGuid(), agentId, null, "Governance fixture run", "manual", "", "", "", "", "",
        "{}", "Input", "Result", "Fixture", "model", ExecutionState.Completed, RunOutcome.Succeeded,
        DateTimeOffset.UnixEpoch.AddMinutes(index), DateTimeOffset.UnixEpoch.AddMinutes(index), null, null, "", null, []);
}

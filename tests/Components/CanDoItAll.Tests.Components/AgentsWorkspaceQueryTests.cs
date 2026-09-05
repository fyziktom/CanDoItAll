using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsWorkspaceQueryTests {
    [Theory]
    [InlineData(AgentWorkspaceSection.Providers)]
    [InlineData(AgentWorkspaceSection.RequestHistory)]
    public async Task History_demand_skips_aggregates_but_keeps_shell_identity(AgentWorkspaceSection section) {
        var query = CreateQuery(out var workspace, out var usage, out var bindings);
        var snapshot = await query.ReadShellAsync(section, ProviderUsageWorkloadSelection.Both);
        Assert.Null(snapshot.Overview);
        Assert.Null(snapshot.Usage);
        Assert.Equal(0, workspace.OverviewReads);
        Assert.Equal(0, usage.Reads);
        Assert.Equal(1, workspace.AgentReads);
        Assert.Equal(1, bindings.Reads);
        Assert.Equal(7, snapshot.BoundResourceCount);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), snapshot.HrAgentError);
        await query.ReadShellAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.Equal(1, workspace.OverviewReads);
        Assert.Equal(1, usage.Reads);
    }

    [Fact]
    public async Task Usage_refresh_does_not_reload_shell() {
        var query = CreateQuery(out var workspace, out var usage, out var bindings);
        var snapshot = await query.ReadUsageAsync(ProviderUsageWorkloadSelection.Agents);
        Assert.Equal(ProviderUsageWorkloadSelection.Agents, snapshot.Selection);
        Assert.Equal(1, usage.Reads);
        Assert.Equal(0, workspace.OverviewReads);
        Assert.Equal(0, workspace.AgentReads);
        Assert.Equal(0, bindings.Reads);
    }

    [Fact]
    public async Task Real_registration_reads_overview_and_bound_resources() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var query = Assert.IsType<AgentsWorkspaceQuery>(harness.Context.Services.GetRequiredService<IAgentsWorkspaceQuery>());
        Assert.IsType<BoundAgentResourceQuery>(harness.Context.Services.GetRequiredService<IBoundAgentResourceQuery>());
        var result = await query.ReadShellAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var expected = await workspace.GetAgentOverviewAsync();
        Assert.Equal(expected.Totals, Assert.IsType<AgentOverviewSnapshot>(result.Overview).Totals);
        Assert.NotNull(result.Usage);
        Assert.Equal(HrAgentIdentity.AgentId, result.HrAgent?.Id);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), result.AvatarImageUrls.Keys);
        Assert.Equal(await harness.Context.Services.GetRequiredService<IBoundAgentResourceQuery>().CountAsync(), result.BoundResourceCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hr_catalog_failure_is_partial_but_overview_failure_is_not(bool failOverview) {
        var query = CreateQuery(out var workspace, out _, out _);
        workspace.FailAgents = true;
        workspace.FailOverview = failOverview;
        if (failOverview) {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                query.ReadShellAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both));
            Assert.Equal("Overview unavailable.", error.Message);
            return;
        }
        var result = await query.ReadShellAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.NotNull(result.Overview);
        Assert.Equal("Catalog unavailable.", result.HrAgentError);
        Assert.Null(result.HrAgent);
    }

    private static AgentsWorkspaceQuery CreateQuery(
        out WorkspaceReads workspace, out UsageReads usage, out BindingReads bindings) {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceReads>();
        workspace = (WorkspaceReads)(object)service;
        usage = new();
        bindings = new();
        return new(service, new ProviderUsageQueryService([usage]), bindings);
    }

    public class WorkspaceReads : DispatchProxy {
        public int OverviewReads { get; private set; }
        public int AgentReads { get; private set; }
        public bool FailAgents { get; set; }
        public bool FailOverview { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentOverviewAsync)) {
                OverviewReads++;
                return FailOverview
                    ? Task.FromException<AgentOverviewSnapshot>(new InvalidOperationException("Overview unavailable."))
                    : Task.FromResult(AgentOverviewSnapshot.Empty);
            }
            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                AgentReads++;
                return FailAgents
                    ? Task.FromException<IReadOnlyList<AgentDefinition>>(new InvalidOperationException("Catalog unavailable."))
                    : Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
            }
            throw new InvalidOperationException($"Unexpected workspace operation {targetMethod.Name}.");
        }
    }

    private sealed class UsageReads : IProviderUsageProjectionSource {
        public string SourceName => nameof(UsageReads);
        public ProviderUsageWorkloadKind WorkloadKind => ProviderUsageWorkloadKind.Agent;
        public int Reads { get; private set; }
        public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return ValueTask.FromResult(new ProviderUsageSourceResult(SourceName, WorkloadKind,
                ProviderUsageSourceState.Complete, [], DateTimeOffset.UnixEpoch));
        }
    }

    private sealed class BindingReads : IBoundAgentResourceQuery {
        public int Reads { get; private set; }
        public Task<int> CountAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(7);
        }
    }
}

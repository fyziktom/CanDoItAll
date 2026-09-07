using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentsWorkspaceQueryTests {
    [Fact]
    public async Task Header_read_is_independent_of_aggregates() {
        var query = CreateQuery(out var workspace, out var usage, out var bindings);
        var snapshot = await query.ReadHeaderAsync();
        Assert.Equal(0, workspace.OverviewReads);
        Assert.Equal(0, usage.Reads);
        Assert.Equal(1, workspace.AgentReads);
        Assert.Equal(1, bindings.Reads);
        Assert.Equal(7, snapshot.BoundResourceCount);
        Assert.Equal(AgentsHeaderFailure.HrAgent, snapshot.Failures);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Header_catalog_failure_does_not_erase_bound_resource_result(bool failOverview) {
        var query = CreateQuery(out var workspace, out _, out _);
        workspace.FailAgents = true;
        workspace.FailOverview = failOverview;
        var header = await query.ReadHeaderAsync();
        Assert.Equal(7, header.BoundResourceCount);
        Assert.Equal(AgentsHeaderFailure.HrAgent | AgentsHeaderFailure.Avatars, header.Failures);
        Assert.Null(header.HrAgent);
        if (failOverview) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => query.ReadOverviewAsync());
        } else {
            Assert.Equal(AgentOverviewTotals.Empty, (await query.ReadOverviewAsync()).Totals);
        }
    }

    [Fact]
    public async Task Bound_failure_preserves_hr_and_avatar_results() {
        var query = CreateQuery(out var workspace, out _, out var bindings);
        workspace.Agents = [Agent(HrAgentIdentity.AgentId, HrAgentIdentity.TemplateKey)];
        bindings.Fail = true;
        var header = await query.ReadHeaderAsync();
        Assert.Equal(HrAgentIdentity.AgentId, header.HrAgent?.Id);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), header.AvatarImageUrls.Keys);
        Assert.Null(header.BoundResourceCount);
        Assert.Equal(AgentsHeaderFailure.BoundResources, header.Failures);
    }

    [Fact]
    public async Task Missing_hr_does_not_erase_other_agent_avatars() {
        var query = CreateQuery(out var workspace, out _, out _);
        var id = Guid.NewGuid();
        workspace.Agents = [Agent(id, "ordinary-agent")];
        var header = await query.ReadHeaderAsync();
        Assert.Null(header.HrAgent);
        Assert.Contains(id.ToString("D"), header.AvatarImageUrls.Keys);
        Assert.Equal(7, header.BoundResourceCount);
        Assert.Equal(AgentsHeaderFailure.HrAgent, header.Failures);
    }

    private static AgentDefinition Agent(Guid id, string templateKey) => new(
        id, "Header fixture", "HR", "Header read fixture", "Stay scoped.",
        AgentLifecycleStatus.Active, null, "fixture-model", AgentWorkloadKind.General,
        AgentChatHistoryMode.FrameworkManaged, 0.2, true, false, "{}", false, templateKey,
        AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static AgentsWorkspaceQuery CreateQuery(
        out WorkspaceReads workspace, out UsageReads usage, out BindingReads bindings) {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceReads>();
        workspace = (WorkspaceReads)(object)service;
        usage = new();
        bindings = new();
        return new(service, new ProviderUsageQueryService([usage]), bindings, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentsWorkspaceQuery>.Instance);
    }

    public class WorkspaceReads : DispatchProxy {
        public int OverviewReads { get; private set; }
        public int AgentReads { get; private set; }
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
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
                    : Task.FromResult(Agents);
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
        public bool Fail { get; set; }
        public int Reads { get; private set; }
        public Task<int> CountAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Fail ? Task.FromException<int>(new IOException("Private bound failure")) : Task.FromResult(7);
        }
    }
}

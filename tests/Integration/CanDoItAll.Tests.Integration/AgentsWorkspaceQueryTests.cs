using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentsWorkspaceQueryTests {
    [Fact]
    public async Task Real_registration_reads_overview_and_bound_resources() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var query = Assert.IsType<AgentsWorkspaceQuery>(scope.ServiceProvider.GetRequiredService<IAgentsWorkspaceQuery>());
        Assert.IsType<BoundAgentResourceQuery>(scope.ServiceProvider.GetRequiredService<IBoundAgentResourceQuery>());
        var result = await query.ReadShellAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var expected = await workspace.GetAgentOverviewAsync();
        Assert.Equal(expected.Totals, Assert.IsType<AgentOverviewSnapshot>(result.Overview).Totals);
        Assert.NotNull(result.Usage);
        Assert.Equal(HrAgentIdentity.AgentId, result.HrAgent?.Id);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), result.AvatarImageUrls.Keys);
        Assert.Equal(await scope.ServiceProvider.GetRequiredService<IBoundAgentResourceQuery>().CountAsync(), result.BoundResourceCount);
    }

}

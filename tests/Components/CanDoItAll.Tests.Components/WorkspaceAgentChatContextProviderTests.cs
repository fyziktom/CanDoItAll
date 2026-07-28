using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components;

public sealed class WorkspaceAgentChatContextProviderTests
{
    [Fact]
    public async Task Provider_uses_path_only_route_and_never_serializes_sensitive_query_values()
    {
        const string sensitiveQuery = "sensitive-partner-search";
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton(new WorkbenchStateService(
            new InMemoryWorkbenchStateStore(),
            Options.Create(new WorkbenchOptions()),
            new SystemClock()));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/processes?q={sensitiveQuery}&runId={Guid.NewGuid():D}");

        using var cut = context.Render<WorkspaceAgentChatContextProvider>();
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/processes").ToScope(AgentChatContextScopeId.Create()));

        var snapshot = Assert.IsType<AgentChatContextSnapshot>(await registry.CaptureAsync());
        var contribution = Assert.IsType<AgentRuntimeTransientContext>(
            AgentChatContextContributionComposer.Compose(snapshot, Guid.NewGuid()));

        Assert.Equal("/processes", snapshot.WorkspacePosition?.Route);
        Assert.DoesNotContain(sensitiveQuery, contribution.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("?q=", contribution.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("runId=", contribution.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_does_not_copy_metadata_from_a_stale_active_tab()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var workbench = new WorkbenchStateService(
            new InMemoryWorkbenchStateStore(),
            Options.Create(new WorkbenchOptions()),
            new SystemClock());
        var staleProjectId = Guid.NewGuid();
        await workbench.InitializeAsync(
        [
            new WorkbenchTabState(
                "stale-project",
                "Stale project",
                $"/projects?projectId={staleProjectId:D}",
                TabKind: WorkbenchTabKinds.ProjectOverview,
                ProjectId: staleProjectId,
                ProjectName: "Stale project")
        ]);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton(workbench);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/crm-hr/directory");

        using var cut = context.Render<WorkspaceAgentChatContextProvider>();
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/crm-hr/directory").ToScope(AgentChatContextScopeId.Create()));

        var generic = Assert.IsType<AgentChatContextSnapshot>(await registry.CaptureAsync());
        Assert.Equal("route:/crm-hr/directory", generic.WorkspacePosition?.TabId);
        Assert.Null(generic.WorkspacePosition?.ProjectId);
        Assert.Null(generic.WorkspacePosition?.ProjectName);

        await workbench.TrackTabAsync(new WorkbenchTabDescriptor(
            "crm-directory",
            "Party directory",
            "/crm-hr/directory"));

        cut.WaitForAssertion(() =>
        {
            var synchronized = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal("crm-directory", synchronized.WorkspacePosition?.TabId);
            Assert.Equal("Party directory", synchronized.WorkspacePosition?.Title);
        });
    }

    private static AgentChatContextSurface CreateSurface(string route)
        => new(
            new AgentChatContextSource(
                new AgentChatContextSourceKind("processes"),
                new AgentChatContextSourceId("processes")),
            "Processes",
            new AgentChatSurfacePosition(
                "processes",
                "workspace",
                "definition",
                route),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
}

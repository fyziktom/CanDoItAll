using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectCalendarPageDatabaseSwitchTests
{
    [Fact]
    public async Task Missing_project_routes_render_a_safe_calendar_recovery_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var missingProjectId = Guid.NewGuid();

        var cut = harness.Context.RenderComponent<ProjectCalendarPage>(
            parameters => parameters.Add(page => page.ProjectId, missingProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project calendar unavailable", cut.Markup);
            Assert.Contains("does not exist in the active database profile", cut.Markup);
            var provider = cut.FindComponent<ProjectStructureAgentChatContextProvider>();
            Assert.Equal(missingProjectId, provider.Instance.ProjectId);
            Assert.Equal(
                AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri),
                provider.Instance.ContextNavigationIdentity);
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.AccessState);
        });

        await Assert.ThrowsAsync<AgentChatContextUnavailableException>(
            async () => await registry.CaptureAsync());

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Open projects", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/projects", navigation.Uri, StringComparison.OrdinalIgnoreCase));
    }
}

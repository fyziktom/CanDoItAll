using Bunit;
using CanDoItAll.Modules.Workbench.Pages;
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
        var missingProjectId = Guid.NewGuid();

        var cut = harness.Context.RenderComponent<ProjectCalendarPage>(
            parameters => parameters.Add(page => page.ProjectId, missingProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project calendar unavailable", cut.Markup);
            Assert.Contains("does not exist in the active database profile", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Open projects", StringComparison.Ordinal))
            .Click();

        Assert.EndsWith("/projects", navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }
}

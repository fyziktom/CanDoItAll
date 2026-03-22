using Bunit;
using CanDoItAll.Modules.Projects.Pages;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectsPageTests
{
    [Fact]
    public async Task Saves_project_from_wizard_first_flow()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.Find("[data-testid='project-name-input']").Change("Wizard Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Wizard Project", cut.Markup);
            Assert.Contains("Project saved", cut.Markup);
        });
    }

    [Fact]
    public async Task Shows_saved_project_as_card_with_dashboard_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.Find("[data-testid='project-name-input']").Change("Card Modal Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Card Modal Project", cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='project-card']"));
            Assert.Contains("Open dashboard tab", cut.Markup);
        });
    }
}

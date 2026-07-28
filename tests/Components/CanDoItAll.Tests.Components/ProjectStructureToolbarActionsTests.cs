using Bunit;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureToolbarActionsTests
{
    [Fact]
    public void Gantt_action_is_explicit_and_invokes_the_host_callback()
    {
        using var context = new BunitContext();
        var invocationCount = 0;
        var cut = context.Render<ProjectStructureToolbarActions>(parameters => parameters
            .Add(component => component.OpenGanttView, () => invocationCount++));

        var action = cut.Find("[data-testid='project-structure-gantt-toggle']");

        Assert.Equal("Gantt", action.GetAttribute("aria-label"));
        Assert.Equal("Gantt: open the interactive project schedule", action.GetAttribute("title"));

        action.Click();

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void Gantt_action_reflects_the_selected_view()
    {
        using var context = new BunitContext();
        var cut = context.Render<ProjectStructureToolbarActions>(parameters => parameters
            .Add(component => component.GanttViewVisible, true));

        var action = cut.Find("[data-testid='project-structure-gantt-toggle']");

        Assert.Contains("is-active", action.ClassList);
    }
}

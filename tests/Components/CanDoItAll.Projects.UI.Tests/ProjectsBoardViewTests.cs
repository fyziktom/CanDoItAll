using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectsBoardViewTests {
    [Fact]
    public void The_host_owned_portfolio_view_selects_its_tab_and_a_tab_click_only_raises_the_intent() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var raised = new List<ProjectsAgentChatView>();
        var cut = context.Render<ProjectsBoard>(parameters => parameters
            .Add(board => board.ProjectSummaries, Array.Empty<ProjectSummary>())
            .Add(board => board.ProjectProjection, ProjectFileFilterProjection.Create([], [], new ProjectFileFilter()))
            .Add(board => board.FilesContent, (RenderFragment<ProjectFileFilterProjection>)(_ => builder => builder.AddContent(0, "Files slot")))
            .Add(board => board.PortfolioView, ProjectsAgentChatView.Files)
            .Add(board => board.PortfolioViewChanged, view => raised.Add(view)));

        Assert.Equal("true", Tab(cut, "Files").GetAttribute("aria-selected"));

        Tab(cut, "Cards").Click();

        Assert.Equal([ProjectsAgentChatView.Cards], raised);
        cut.Render(parameters => parameters.Add(board => board.PortfolioView, ProjectsAgentChatView.Cards));
        Assert.Equal("true", Tab(cut, "Cards").GetAttribute("aria-selected"));

        cut.Render(parameters => parameters.Add(board => board.PortfolioView, ProjectsAgentChatView.Files));
        Assert.Equal("true", Tab(cut, "Files").GetAttribute("aria-selected"));
    }

    private static AngleSharp.Dom.IElement Tab(IRenderedComponent<ProjectsBoard> cut, string label)
        => cut.Find("[data-testid='projects-portfolio-tabs']").QuerySelectorAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains(label, StringComparison.Ordinal));
}

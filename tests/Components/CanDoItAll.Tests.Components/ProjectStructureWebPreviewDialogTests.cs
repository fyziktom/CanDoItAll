using Bunit;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureWebPreviewDialogTests
{
    [Fact]
    public void Embeddable_link_shows_notes_in_a_restricted_iframe()
    {
        using var context = new TestContext();
        var state = new ProjectStructureWebPreviewDialogState(
            "node-1",
            "Architecture guide",
            "Web link",
            "https://docs.example.com/architecture",
            "Review section three.",
            CanEmbed: true,
            EmbedUnavailableReason: string.Empty);

        var cut = context.RenderComponent<ProjectStructureWebPreviewDialog>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Close, EventCallback.Empty)
            .Add(component => component.OpenInBrowser, EventCallback.Empty));

        var frame = cut.Find("iframe");
        Assert.Equal(state.Url, frame.GetAttribute("src"));
        Assert.Equal("allow-forms allow-popups allow-scripts", frame.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.Contains("Review section three.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Known_non_embeddable_link_uses_the_explicit_browser_fallback()
    {
        using var context = new TestContext();
        var state = new ProjectStructureWebPreviewDialogState(
            "node-1",
            "Repository",
            "Repository",
            "https://github.com/example/project",
            string.Empty,
            CanEmbed: false,
            EmbedUnavailableReason: "GitHub does not allow repository pages to be embedded.");

        var cut = context.RenderComponent<ProjectStructureWebPreviewDialog>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Close, EventCallback.Empty)
            .Add(component => component.OpenInBrowser, EventCallback.Empty));

        Assert.Empty(cut.FindAll("iframe"));
        Assert.Contains(state.EmbedUnavailableReason, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(
            cut.FindAll("button"),
            button => button.TextContent.Contains("Open in browser", StringComparison.Ordinal));
    }
}

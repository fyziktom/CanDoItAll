using Bunit;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureWebPreviewDialogTests
{
    [Fact]
    public void Embeddable_link_shows_notes_in_a_restricted_iframe()
    {
        using var context = new BunitContext();
        var state = new ProjectStructureWebPreviewDialogState(
            "node-1",
            "Architecture guide",
            "Web link",
            new Uri("https://docs.example.com/architecture"),
            "Review section three.",
            CanEmbed: true,
            EmbedUnavailableReason: string.Empty);

        var cut = context.Render<ProjectStructureWebPreviewDialog>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Close, EventCallback.Empty));

        var frame = cut.Find("iframe");
        Assert.Equal(state.Url.AbsoluteUri, frame.GetAttribute("src"));
        var sandbox = frame.GetAttribute("sandbox");
        Assert.Equal("allow-forms allow-popups allow-scripts", sandbox);
        Assert.DoesNotContain("allow-same-origin", sandbox, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-top-navigation", sandbox, StringComparison.Ordinal);
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.Contains("Review section three.", cut.Markup, StringComparison.Ordinal);

        var notes = cut.Find("[data-testid='project-structure-web-preview-notes']");
        Assert.Contains("max-height:min(12rem,35%)", notes.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("overflow:auto", notes.GetAttribute("style"), StringComparison.Ordinal);

        var externalLink = cut.Find("[data-testid='project-structure-web-preview-open-browser']");
        Assert.Equal("a", externalLink.LocalName);
        Assert.Equal(state.Url.AbsoluteUri, externalLink.GetAttribute("href"));
        Assert.Equal("_blank", externalLink.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", externalLink.GetAttribute("rel"));
    }

    [Fact]
    public void Known_non_embeddable_link_uses_the_explicit_browser_fallback()
    {
        using var context = new BunitContext();
        var state = new ProjectStructureWebPreviewDialogState(
            "node-1",
            "Repository",
            "Repository",
            new Uri("https://github.com/example/project"),
            string.Empty,
            CanEmbed: false,
            EmbedUnavailableReason: "GitHub does not allow repository pages to be embedded.");

        var cut = context.Render<ProjectStructureWebPreviewDialog>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Close, EventCallback.Empty));

        Assert.Empty(cut.FindAll("iframe"));
        Assert.Contains(state.EmbedUnavailableReason, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(
            cut.FindAll("a"),
            link => link.TextContent.Contains("Open in browser", StringComparison.Ordinal));
    }
}

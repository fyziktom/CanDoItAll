using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class PresentationBadgeListTests
{
    [Fact]
    public void Badge_list_renders_typed_tones_icon_and_accessibility_description()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        PresentationBadge[] badges =
        [
            new("Ready", PresentationTone.Success, "check_circle", "Ready for execution"),
            new("Needs review", PresentationTone.Warning)
        ];

        var cut = context.Render<PresentationBadgeList>(parameters => parameters
            .Add(component => component.Badges, badges)
            .Add(component => component.TestId, "test-badges"));

        var shell = cut.Find("[data-testid='test-badges']");
        Assert.Equal(2, shell.QuerySelectorAll(".cda-badge").Length);
        Assert.Contains("cda-badge--tone-success", shell.QuerySelectorAll(".cda-badge")[0].ClassList);
        Assert.Contains("cda-badge--tone-warning", shell.QuerySelectorAll(".cda-badge")[1].ClassList);
        Assert.Equal("Ready for execution", shell.QuerySelector(".cda-badge")?.GetAttribute("title"));
        Assert.Equal("check_circle", shell.QuerySelector(".material-symbols-rounded")?.TextContent.Trim());
    }

    [Fact]
    public void Empty_badge_list_does_not_render_an_empty_layout_shell()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<PresentationBadgeList>(parameters => parameters
            .Add(component => component.Badges, []));

        Assert.Empty(cut.Markup);
    }
}

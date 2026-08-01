using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class QuickActionCardTests
{
    [Fact]
    public void Renders_native_compact_anchor_with_typed_layout_and_content()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<QuickActionCard>(parameters => parameters
            .Add(component => component.Href, "/projects")
            .Add(component => component.Icon, "folder_open")
            .Add(component => component.Label, "Projects")
            .Add(component => component.TestId, "quick-action-projects"));

        var anchor = Assert.Single(cut.FindAll("a"));
        Assert.Equal("/projects", anchor.GetAttribute("href"));
        Assert.Equal("Projects", anchor.GetAttribute("aria-label"));
        Assert.Equal("quick-action-projects", anchor.GetAttribute("data-testid"));
        Assert.Contains("Projects", anchor.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button"));

        var actionCard = cut.FindComponent<ActionCard>();
        Assert.Contains("h-full", actionCard.Instance.Class, StringComparison.Ordinal);

        var stack = cut.FindComponent<Stack>();
        Assert.Equal(AlignItems.Center, stack.Instance.AlignItems);
        Assert.Equal(JustifyContent.Center, stack.Instance.JustifyContent);
        Assert.Contains("aspect-square", stack.Instance.Class, StringComparison.Ordinal);

        Assert.Equal("folder_open", cut.FindComponent<Icon>().Instance.Name);
        Assert.Equal("Projects", cut.FindComponent<TextBlock>().Instance.Value);
    }
}

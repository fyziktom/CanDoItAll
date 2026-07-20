using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentAvatarActionButtonTests
{
    [Fact]
    public void Action_uses_avatar_accessible_label_and_bottom_service_tooltip_without_visible_text()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var clickCount = 0;
        var cut = context.RenderComponent<AgentAvatarActionButton>(parameters => parameters
            .Add(component => component.AgentName, "Workflow Curator Agent")
            .Add(component => component.AvatarImageUrl, "/avatars/workflow-curator.jpg")
            .Add(component => component.FallbackText, "WC")
            .Add(component => component.Label, "Open Workflow Curator Agent")
            .Add(component => component.TestId, "agent-avatar-action")
            .Add(component => component.TooltipTestId, "agent-avatar-tooltip")
            .Add(component => component.Click, EventCallback.Factory.Create(this, () => clickCount++)));

        var button = cut.Find("[data-testid='agent-avatar-action']");
        Assert.Equal("Open Workflow Curator Agent", button.GetAttribute("aria-label"));
        Assert.True(string.IsNullOrWhiteSpace(button.TextContent));
        var avatar = Assert.IsAssignableFrom<IElement>(button.QuerySelector("img"));
        Assert.Equal("Workflow Curator Agent", avatar.GetAttribute("alt"));
        Assert.Equal("/avatars/workflow-curator.jpg", avatar.GetAttribute("src"));

        Assert.IsAssignableFrom<IElement>(button.ParentElement)
            .TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 100, ClientY = 32 });
        var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal("Open Workflow Curator Agent", tooltip?.Text);
        Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
        Assert.Equal("agent-avatar-tooltip", tooltip?.Options.TestId);

        button.Click();
        Assert.Equal(1, clickCount);
    }
}

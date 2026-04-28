using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class TooltipServiceTests
{
    [Fact]
    public void TooltipHost_renders_service_tooltip_at_pointer_position()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<TooltipService>();
        var host = context.RenderComponent<Tooltip>();

        service.Open(
            "Explain the approval state",
            240,
            180,
            new TooltipOptions
            {
                Position = TooltipPosition.Right,
                Duration = null,
                TestId = "approval-tooltip"
            });

        host.WaitForAssertion(() =>
        {
            var tooltip = host.Find("[data-testid='approval-tooltip']");
            Assert.Contains("Explain the approval state", tooltip.TextContent, StringComparison.Ordinal);
            Assert.Equal("tooltip", tooltip.GetAttribute("role"));
            Assert.Contains("left:clamp(1rem,240px", tooltip.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("translate(0.75rem,-50%)", tooltip.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TooltipService_close_clears_visible_tooltip()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<TooltipService>();
        var host = context.RenderComponent<Tooltip>();

        service.Open("Short help", 40, 40, new TooltipOptions { Duration = null, TestId = "short-help" });
        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='short-help']")));

        service.Close();

        host.WaitForAssertion(() => Assert.DoesNotContain("Short help", host.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void TooltipHost_supports_corner_positions()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<TooltipService>();
        var host = context.RenderComponent<Tooltip>();

        service.Open(
            "Corner help",
            300,
            220,
            new TooltipOptions
            {
                Position = TooltipPosition.TopLeft,
                Duration = null,
                TestId = "corner-tooltip"
            });

        host.WaitForAssertion(() =>
        {
            var tooltip = host.Find("[data-testid='corner-tooltip']");
            Assert.Contains("Corner help", tooltip.TextContent, StringComparison.Ordinal);
            Assert.Contains("translate(calc(-100% - 0.75rem),calc(-100% - 0.75rem))", tooltip.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TooltipTarget_opens_and_closes_tooltip_from_mouse_events()
    {
        using var context = CreateContext();
        var host = context.Render(builder =>
        {
            builder.OpenComponent<Tooltip>(0);
            builder.CloseComponent();
            builder.OpenComponent<TooltipTarget>(1);
            builder.AddAttribute(2, "Text", "Target tooltip");
            builder.AddAttribute(3, "TestId", "target-tooltip");
            builder.AddAttribute(4, "Duration", (TimeSpan?)null);
            builder.AddAttribute(5, "TriggerClass", "tooltip-trigger");
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(contentBuilder => contentBuilder.AddContent(0, "Hover target")));
            builder.CloseComponent();
        });

        host.Find(".tooltip-trigger").TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 120, ClientY = 90 });
        host.WaitForAssertion(() => Assert.Contains("Target tooltip", host.Markup, StringComparison.Ordinal));

        host.Find(".tooltip-trigger").TriggerEvent("onmouseleave", new MouseEventArgs());
        host.WaitForAssertion(() => Assert.DoesNotContain("Target tooltip", host.Markup, StringComparison.Ordinal));
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}

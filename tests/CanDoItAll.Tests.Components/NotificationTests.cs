using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class NotificationTests
{
    [Fact]
    public void Notification_surface_renders_above_modal_overlays()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.RenderComponent<Notification>();
        var root = cut.Find(".rz-notification");
        var style = (root.GetAttribute("style") ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

        Assert.Contains("z-index:900", style, StringComparison.Ordinal);
        Assert.Contains("z-[90]", root.ClassList);
    }

    [Fact]
    public void NotificationService_notify_overload_tracks_payload_and_callbacks()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        var service = context.Services.GetRequiredService<NotificationService>();
        var clicked = false;
        var closed = false;
        var payload = new { Id = 42 };

        var message = service.Notify(
            NotificationSeverity.Success,
            "Saved",
            "The changes are ready.",
            duration: 0,
            click: clickedMessage =>
            {
                clicked = true;
                Assert.Same(payload, clickedMessage.Payload);
            },
            closeOnClick: true,
            payload,
            close: _ => closed = true);

        Assert.Single(service.Messages);
        service.Click(message);

        Assert.True(clicked);
        Assert.True(closed);
        Assert.Empty(service.Messages);
    }

    [Fact]
    public void NotificationHost_renders_and_dismisses_service_messages()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        var service = context.Services.GetRequiredService<NotificationService>();
        var cut = context.RenderComponent<Notification>();
        var closed = false;

        service.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Warning,
            Summary = "Needs review",
            Detail = "The dialog proof is still pending.",
            Duration = 0,
            TestId = "review-toast",
            Close = _ => closed = true
        });

        cut.WaitForAssertion(() =>
        {
            var toast = cut.Find("[data-testid='review-toast']");
            Assert.Contains("Needs review", toast.TextContent, StringComparison.Ordinal);
            Assert.Contains("The dialog proof is still pending.", toast.TextContent, StringComparison.Ordinal);
            Assert.Equal("X", toast.QuerySelector("button")?.TextContent.Trim());
        });

        cut.Find("[data-testid='review-toast'] button").Click();

        Assert.True(closed);
        Assert.Empty(service.Messages);
    }

    [Fact]
    public void NotificationHost_places_messages_by_position()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        var service = context.Services.GetRequiredService<NotificationService>();
        var cut = context.RenderComponent<Notification>();

        service.Notify(
            NotificationSeverity.Success,
            "Bottom center",
            "This toast should be centered near the bottom edge.",
            duration: 0,
            position: NotificationPosition.BottomCenter);

        cut.WaitForAssertion(() =>
        {
            var stack = cut.Find("[data-notification-position='BottomCenter']");
            Assert.Contains("bottom-4", stack.ClassList);
            Assert.Contains("left-1/2", stack.ClassList);
            Assert.Contains("-translate-x-1/2", stack.ClassList);
            Assert.Contains("Bottom center", stack.TextContent, StringComparison.Ordinal);
        });
    }
}

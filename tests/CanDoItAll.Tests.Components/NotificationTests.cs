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
        context.Services.AddScoped<NotificationService>();

        var cut = context.RenderComponent<Notification>();
        var root = cut.Find(".rz-notification");
        var style = (root.GetAttribute("style") ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

        Assert.Contains("z-index:900", style, StringComparison.Ordinal);
        Assert.Contains("z-[90]", root.ClassList);
    }
}

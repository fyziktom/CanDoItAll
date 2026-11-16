using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class AppToolbarOwnershipTests : BunitContext {
    [Fact]
    public void Disposing_previous_page_cannot_clear_current_page_actions_stats_or_help() {
        var toolbar = new AppToolbarState(Services.GetRequiredService<IJSRuntime>());
        var previous = new object();
        var current = new object();
        RenderFragment actions = builder => builder.AddContent(0, "Save current");
        RenderFragment stats = builder => builder.AddContent(0, "Current count");
        toolbar.Register(previous, new("Previous", "Previous help", string.Empty, true, null, null));
        toolbar.Register(current, new("Current", "Current help", string.Empty, true, actions, stats));

        toolbar.Clear(previous);

        Assert.Equal("Current", toolbar.HelpHeading);
        Assert.Same(actions, toolbar.ActionsContent);
        Assert.Same(stats, toolbar.StatsContent);
        Assert.NotNull(toolbar.HelpContent);

        toolbar.Clear(current);

        Assert.Null(toolbar.HelpContent);
        Assert.Null(toolbar.ActionsContent);
        Assert.Null(toolbar.StatsContent);
    }
}

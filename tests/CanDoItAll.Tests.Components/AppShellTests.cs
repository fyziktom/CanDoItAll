using Bunit;
using CanDoItAll.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AppShellTests : TestContext
{
    public AppShellTests()
    {
        Services.AddSingleton<TooltipService>();
    }

    [Fact]
    public void Renders_more_continuation_panel_when_desktop_navigation_overflows()
    {
        var cut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.CurrentRoute, "automation")
            .Add(component => component.TopBar, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Shell status</div>"))));

        Assert.Contains("app-shell-workbar", cut.Markup);
        Assert.Contains("shell-nav-more", cut.Markup);
        Assert.Contains("more_up", cut.Markup);
        Assert.Contains("shell-nav-overflow-panel", cut.Markup);
        Assert.Contains("shell-nav-overflow-test-lab", cut.Markup);
        Assert.Contains(">Tests<", cut.Markup);
        Assert.Contains("shell-nav-automation", cut.Markup);
    }
}

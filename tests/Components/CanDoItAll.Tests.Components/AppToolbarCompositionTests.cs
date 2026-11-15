using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class AppToolbarCompositionTests : BunitContext {
    public AppToolbarCompositionTests() {
        Services.AddCanDoItAllBaseLib();
        Services.AddScoped<AppToolbarState>();
        Services.AddSingleton<IUserUiPreferencesState>(new UserUiPreferencesTestState());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("false")]
    public void A_new_page_starts_with_help_closed_regardless_of_the_old_help_cookie(string? helpCookie) {
        var toolbar = Services.GetRequiredService<AppToolbarState>();
        Assert.False(toolbar.HelpVisible);

        toolbar.InitializeFromCookies(helpCookie, null);
        toolbar.Register(new object(), new("Projects", "Browse your projects.", string.Empty, true, null, null));

        Assert.False(toolbar.HelpVisible);
        Assert.True(toolbar.StatsVisible);
    }

    [Fact]
    public void A_saved_statistics_preference_is_preserved() {
        var toolbar = Services.GetRequiredService<AppToolbarState>();

        toolbar.InitializeFromCookies(null, "false");
        toolbar.Register(new object(), new("Projects", "Browse your projects.", string.Empty, true, null, Text("15 projects")));

        Assert.False(toolbar.StatsVisible);
        Assert.NotNull(toolbar.StatsContent);
    }

    [Fact]
    public async Task A_failed_statistics_preference_write_keeps_the_confirmed_visibility() {
        var toolbar = Services.GetRequiredService<AppToolbarState>();
        JSInterop.SetupVoid("CanDoItAll.browserState.saveCookie", AppToolbarState.StatsVisibleCookieName, "false")
            .SetException(new JSException("Browser storage is unavailable."));
        var notifications = 0;
        toolbar.Changed += () => notifications++;

        await Assert.ThrowsAsync<JSException>(() => toolbar.SetStatsVisibleAsync(false));

        Assert.True(toolbar.StatsVisible);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Help_survives_same_owner_updates_but_closes_when_the_page_owner_changes(bool usePageHeader) {
        var toolbar = Services.GetRequiredService<AppToolbarState>();
        var previous = new object();
        var current = new object();
        SetHelp(previous, "Projects", "Browse your projects.");
        await toolbar.SetHelpVisibleAsync(true);

        SetHelp(previous, "Projects", "Updated project guidance.");
        Assert.True(toolbar.HelpVisible);

        SetHelp(current, "Agents", "Browse your colleagues.");
        Assert.False(toolbar.HelpVisible);
        Assert.Equal("Agents", toolbar.HelpHeading);

        await toolbar.SetHelpVisibleAsync(true);
        toolbar.Clear(previous);
        Assert.True(toolbar.HelpVisible);
        Assert.NotNull(toolbar.HelpContent);

        toolbar.Clear(current);
        Assert.False(toolbar.HelpVisible);
        Assert.Null(toolbar.HelpContent);
        Assert.Empty(JSInterop.Invocations);

        void SetHelp(object owner, string title, string description) {
            if (usePageHeader) {
                toolbar.Register(owner, new(title, description, string.Empty, true, null, null));
                return;
            }
            toolbar.SetHelp(owner, title, Text(description));
        }
    }

    [Fact]
    public async Task Statistics_are_available_in_the_toolbar_and_can_be_hidden_and_restored() {
        var toolbar = Services.GetRequiredService<AppToolbarState>();
        toolbar.Register(new object(), new("Projects", "Browse your projects.", string.Empty, true, null, Text("15 projects")));
        var cut = Render<AppToolbar>(parameters => parameters.Add(component => component.PageText, "Projects"));

        Assert.Contains("15 projects", cut.Find("[role='banner']").TextContent, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Toggle page statistics']").ClickAsync());
        cut.WaitForAssertion(() => Assert.DoesNotContain("15 projects", cut.Find("[role='banner']").TextContent, StringComparison.Ordinal));
        Assert.NotNull(toolbar.StatsContent);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Toggle page statistics']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Contains("15 projects", cut.Find("[role='banner']").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Help_opens_on_request_and_closes_through_the_dialog_without_losing_its_content() {
        var toolbar = Services.GetRequiredService<AppToolbarState>();
        toolbar.Register(new object(), new("Projects", "Browse your projects.", string.Empty, true, null, null));
        var header = Render<AppToolbar>(parameters => parameters.Add(component => component.PageText, "Projects"));
        var help = Render<AppToolbarOutlet>(parameters => parameters.Add(component => component.Kind, AppToolbarOutletKind.Help));
        Assert.Empty(help.FindAll("dialog"));

        await header.InvokeAsync(() => header.Find("button[aria-label='Toggle page help']").ClickAsync());
        help.WaitForAssertion(() => Assert.Contains("Browse your projects.", help.Find("dialog").TextContent, StringComparison.Ordinal));
        Assert.Equal("Projects", help.FindComponent<Dialog>().Instance.Title);

        await help.InvokeAsync(() => help.Find("button[aria-label='Close']").ClickAsync());
        help.WaitForAssertion(() => Assert.Empty(help.FindAll("dialog")));
        Assert.False(toolbar.HelpVisible);
        Assert.NotNull(toolbar.HelpContent);

        await header.InvokeAsync(() => header.Find("button[aria-label='Toggle page help']").ClickAsync());
        help.WaitForAssertion(() => Assert.Single(help.FindAll("dialog")));
        await header.InvokeAsync(() => toolbar.Register(new object(), new("Agents", "Browse your colleagues.", string.Empty, true, null, null)));
        help.WaitForAssertion(() => Assert.Empty(help.FindAll("dialog")));
        Assert.Equal("Agents", toolbar.HelpHeading);
    }

    private static RenderFragment Text(string value) => builder => builder.AddContent(0, value);
}

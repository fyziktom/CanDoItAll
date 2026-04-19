using Bunit;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WebMainLayout = CanDoItAll.Web.Components.Layout.MainLayout;

namespace CanDoItAll.Tests.Components;

public sealed class MainLayoutDatabaseProfileTests
{
    [Fact]
    public async Task Main_layout_renders_active_database_indicator_and_startup_modal()
    {
        await using var harness = await CreateUnlockedHarnessAsync();

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Active database", cut.Markup);
            Assert.Contains("Managed SQLite workspace", cut.Markup);
            Assert.Contains("database-startup-modal", cut.Markup);
            Assert.Contains("database-startup-continue", cut.Markup);
            Assert.Contains("database-startup-create-managed", cut.Markup);
            Assert.Contains("database-startup-open-settings", cut.Markup);
            Assert.DoesNotContain("data-testid=\"layout-body\"", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Continue with the active database to load the workspace", cut.Markup);
        });
    }

    [Fact]
    public async Task Main_layout_reopens_database_switcher_from_top_bar()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed")
            .SetResult(true);

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-topbar-switcher", cut.Markup);
            Assert.DoesNotContain("database-startup-modal", cut.Markup);
        });

        cut.Find("[data-testid='database-topbar-switcher']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-switcher-dialog", cut.Markup);
            Assert.Contains("database-startup-create-managed", cut.Markup);
            Assert.Contains("Already active", cut.Markup);
        });
    }

    [Fact]
    public async Task Main_layout_renders_routed_body_after_startup_database_prompt_is_dismissed()
    {
        await using var harness = await CreateUnlockedHarnessAsync();

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForElement("[data-testid='database-startup-continue']");
        cut.Find("[data-testid='database-startup-continue']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("data-testid=\"layout-body\"", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("database-startup-modal", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Main_layout_database_dialog_renders_copy_buttons_for_visible_database_targets()
    {
        await using var harness = await CreateUnlockedHarnessAsync();

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='database-dialog-copy-active-target']"));
            Assert.NotNull(cut.Find("[data-testid='database-dialog-copy-workspace-root']"));
            Assert.Single(cut.FindAll("[data-testid^='database-dialog-copy-profile-']"));
        });
    }

    [Fact]
    public async Task Main_layout_hosts_notification_surface_for_runtime_toasts()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed")
            .SetResult(true);

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            var notificationHost = cut.Find(".rz-notification");
            var style = (notificationHost.GetAttribute("style") ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

            Assert.Contains("z-index:900", style, StringComparison.Ordinal);
        });
    }

    private static async Task<ComponentTestHarness> CreateUnlockedHarnessAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-layout-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("bootstrap");

        return await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
                ["Database:Provider"] = null,
                ["Database:ConnectionString"] = null
            }
        });
    }
}

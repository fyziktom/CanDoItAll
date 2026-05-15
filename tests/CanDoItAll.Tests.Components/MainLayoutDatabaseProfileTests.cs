using Bunit;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WebMainLayout = CanDoItAll.Web.Components.Layout.MainLayout;

namespace CanDoItAll.Tests.Components;

public sealed class MainLayoutDatabaseProfileTests
{
    [Fact]
    public async Task Main_layout_renders_body_without_startup_modal_for_persisted_active_database()
    {
        await using var harness = await CreatePersistedActiveHarnessAsync();

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-shell-action", cut.Markup);
            Assert.Contains("shell-settings-action", cut.Markup);
            Assert.Contains("data-testid=\"layout-body\"", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("active-database-indicator", cut.Markup);
            Assert.DoesNotContain("database-topbar-switcher", cut.Markup);
            Assert.DoesNotContain("database-startup-modal", cut.Markup);
            Assert.DoesNotContain("Continue with the active database to load the workspace", cut.Markup);
        });
    }

    [Fact]
    public async Task Main_layout_renders_startup_modal_for_runtime_database_override()
    {
        await using var harness = await CreateRuntimeOverrideHarnessAsync();

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-startup-modal", cut.Markup);
            Assert.Contains("database-startup-continue", cut.Markup);
            Assert.Contains("database-startup-create-managed", cut.Markup);
            Assert.Contains("database-startup-open-settings", cut.Markup);
            Assert.DoesNotContain("data-testid=\"layout-body\"", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Continue with the active database to load the workspace", cut.Markup);
        });
    }

    [Fact]
    public async Task Main_layout_reopens_database_switcher_from_shell_utility()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed")
            .SetResult(true);

        var cut = harness.Context.RenderComponent<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div data-testid=\"layout-body\">Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-shell-action", cut.Markup);
            Assert.DoesNotContain("database-topbar-switcher", cut.Markup);
            Assert.DoesNotContain("database-startup-modal", cut.Markup);
        });

        cut.Find("[data-testid='database-shell-action']").Click();

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
        await using var harness = await CreateRuntimeOverrideHarnessAsync();

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
        await using var harness = await CreateRuntimeOverrideHarnessAsync();

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

    private static async Task<ComponentTestHarness> CreatePersistedActiveHarnessAsync()
    {
        var harness = await CreateUnlockedHarnessAsync();
        var profileService = harness.Context.Services.GetRequiredService<IDatabaseProfileService>();
        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Persisted active SQLite workspace",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });
        Assert.True(saveResult.IsSuccess);

        var activateResult = await profileService.ActivateAsync(saveResult.Value);
        Assert.True(activateResult.IsSuccess);
        var selection = await profileService.GetCurrentSelectionAsync();
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, selection.ResolutionSource);
        return harness;
    }

    private static async Task<ComponentTestHarness> CreateRuntimeOverrideHarnessAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-layout-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("bootstrap");

        var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath
            }
        });
        var profileService = harness.Context.Services.GetRequiredService<IDatabaseProfileService>();
        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Configured SQLite override",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
            SqliteDatabasePath = activeProfile.DatabasePath,
            WorkspaceRoot = activeProfile.WorkspaceRootPath
        });
        Assert.True(saveResult.IsSuccess);
        return harness;
    }
}

using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workspace.StorageCatalog.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class SettingsPageUrlStateTests {
    [Fact]
    public async Task Tab_intents_push_canonical_locations_and_external_locations_select_the_tab() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/settings?tab=Files&utm=mail");

        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid='file-application-extension']");
        Assert.Equal("http://localhost/settings?tab=files&utm=mail", navigation.Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => Tab(cut, "Secrets").ClickAsync(new()));
        Assert.Equal("http://localhost/settings?tab=secrets&utm=mail", navigation.Uri);
        Assert.Equal(entries + 1, navigation.History.Count);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);

        await cut.InvokeAsync(() => navigation.NavigateTo("/settings?tab=provider-history"));
        cut.WaitForElement("[data-testid='history-policy-panel']");

        await cut.InvokeAsync(() => navigation.NavigateTo("/settings?tab=invalid"));
        cut.WaitForElement("[data-testid='defaults-name']");
        Assert.Equal("http://localhost/settings", navigation.Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task Providers_tab_is_a_cross_page_link_that_pushes_the_agents_route() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/settings?tab=files");
        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid='file-application-extension']");
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => Tab(cut, "Providers").ClickAsync(new()));

        Assert.Equal("http://localhost/agents?tab=providers", navigation.Uri);
        Assert.Equal(entries + 1, navigation.History.Count);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task A_deep_linked_secret_is_selected_and_its_identity_follows_tab_switches_and_new() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var secretId = await SaveSecretAsync(harness, "Addressable secret");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/settings?tab=secrets&secretId={secretId:D}");

        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForAssertion(() => {
            Assert.Equal("Addressable secret", cut.Find("[data-testid='secret-name']").GetAttribute("value"));
            Assert.Single(cut.FindAll(".cda-selection-list-item__button"),
                button => button.TextContent.Contains("Addressable secret", StringComparison.Ordinal));
        });
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => Tab(cut, "Files").ClickAsync(new()));
        Assert.Equal("http://localhost/settings?tab=files", navigation.Uri);
        await cut.InvokeAsync(() => Tab(cut, "Secrets").ClickAsync(new()));
        Assert.Equal($"http://localhost/settings?tab=secrets&secretId={secretId:D}", navigation.Uri);
        Assert.Equal("Addressable secret", cut.Find("[data-testid='secret-name']").GetAttribute("value"));
        Assert.Equal(entries + 2, navigation.History.Count);

        await cut.InvokeAsync(() => cut.FindAll("button").First(button => button.TextContent.Contains("New secret", StringComparison.Ordinal)).ClickAsync(new()));
        Assert.Equal("http://localhost/settings?tab=secrets", navigation.Uri);
        Assert.Equal(entries + 3, navigation.History.Count);

        await cut.InvokeAsync(() => cut.FindAll(".cda-selection-list-item__button")
            .Single(button => button.TextContent.Contains("Addressable secret", StringComparison.Ordinal)).ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal($"http://localhost/settings?tab=secrets&secretId={secretId:D}", navigation.Uri));
        Assert.Equal(entries + 4, navigation.History.Count);

        await cut.InvokeAsync(() => navigation.NavigateTo("/settings?tab=secrets"));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Addressable secret", cut.Find("[data-testid='secret-name']").GetAttribute("value") ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_runtime_data_source_is_replaced_into_the_location_and_operator_selection_pushes() {
        await using var harness = await CreateUnlockedHarnessAsync();
        var profiles = harness.Context.Services.GetRequiredService<DatabaseProfileWorkspaceService>();
        var saved = await SaveProfileAsync(harness, profiles);
        var runtime = (await profiles.GetCurrentSelectionAsync()).RuntimeProfileId;
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/settings?tab=data-sources");
        var entries = navigation.History.Count;

        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForAssertion(() => Assert.Equal($"http://localhost/settings?tab=data-sources&profileId={runtime:D}", navigation.Uri));
        Assert.Equal(entries, navigation.History.Count);

        await cut.InvokeAsync(() => cut.Find($"[data-testid='database-profile-row-{saved:N}']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal("db.internal", cut.Find("[data-testid='database-profile-postgres-host']").GetAttribute("value")));
        Assert.Equal($"http://localhost/settings?tab=data-sources&profileId={saved:D}", navigation.Uri);
        Assert.Equal(entries + 1, navigation.History.Count);
    }

    [Fact]
    public async Task An_explicit_data_source_wins_over_the_runtime_default_and_a_missing_one_stays_unavailable() {
        await using var harness = await CreateUnlockedHarnessAsync();
        var profiles = harness.Context.Services.GetRequiredService<DatabaseProfileWorkspaceService>();
        var saved = await SaveProfileAsync(harness, profiles);
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/settings?tab=data-sources&profileId={saved:D}");

        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForAssertion(() => Assert.Equal("db.internal", cut.Find("[data-testid='database-profile-postgres-host']").GetAttribute("value")));
        Assert.Equal($"http://localhost/settings?tab=data-sources&profileId={saved:D}", navigation.Uri);

        var missing = Guid.NewGuid();
        await cut.InvokeAsync(() => navigation.NavigateTo($"/settings?tab=data-sources&profileId={missing:D}"));
        cut.WaitForElement("[data-testid='database-profile-read-error']");
        Assert.Equal($"http://localhost/settings?tab=data-sources&profileId={missing:D}", navigation.Uri);
    }

    [Fact]
    public async Task Storage_target_selection_pushes_and_locations_select_the_exact_target() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/settings?tab=storage");
        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid=storage-settings-name]");
        var session = cut.FindComponent<CatalogSurface>().Instance.Session;
        cut.WaitForAssertion(() => Assert.True(session.Catalog.IsAvailable));
        var system = Assert.Single(session.Catalog.Value, row => row.IsSystemDefault);
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => cut.Find($"[data-testid=storage-catalog-row-{system.Id:N}]").ClickAsync(new()));
        Assert.Equal($"http://localhost/settings?tab=storage&storageId={system.Id:D}", navigation.Uri);
        Assert.Equal(entries + 1, navigation.History.Count);

        await cut.InvokeAsync(() => navigation.NavigateTo("/settings?tab=storage"));
        cut.WaitForAssertion(() => Assert.Null(session.SelectedId));

        await cut.InvokeAsync(() => navigation.NavigateTo($"/settings?tab=storage&storageId={system.Id:D}"));
        cut.WaitForAssertion(() => Assert.Equal(system.Id, session.SelectedId));
    }

    [Fact]
    public async Task Account_search_is_committed_by_the_explicit_search_action_and_typing_does_not_navigate() {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.EnableUi().AddSingleton<IApiTokenAdministrationAccess>(new AllowedAccess()));
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/settings?tab=api-access&q=component");

        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Search “component”", cut.Find("[data-testid=api-users-page]").TextContent, StringComparison.Ordinal));
        Assert.Equal("component", cut.Find("[data-testid=api-users-search]").GetAttribute("value"));
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => cut.Find("[data-testid=api-users-search]").Input("other"));
        Assert.Equal(entries, navigation.History.Count);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-users-search-submit]").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/settings?tab=api-access&q=other", navigation.Uri));
        Assert.Equal(entries + 1, navigation.History.Count);
        Assert.Contains("Search “other”", cut.Find("[data-testid=api-users-page]").TextContent, StringComparison.Ordinal);
    }

    private static BunitNavigationManager Navigation(ComponentTestHarness harness)
        => Assert.IsType<BunitNavigationManager>(harness.Context.Services.GetRequiredService<NavigationManager>());

    private static IElement Tab(IRenderedComponent<SettingsPage> cut, string label)
        => cut.FindComponent<SecondaryTabs>().FindAll("button").Single(button => button.TextContent.Trim().StartsWith(label, StringComparison.Ordinal));

    private static async Task<Guid> SaveSecretAsync(ComponentTestHarness harness, string name) {
        var result = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = name,
            Kind = SecretKind.ApiKey,
            SecretValue = "component-url-secret",
            Scope = "workspace"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> SaveProfileAsync(ComponentTestHarness harness, DatabaseProfileWorkspaceService profiles) {
        var result = await profiles.SaveProfileAsync(new DatabaseProfileEditorModel {
            DisplayName = "Addressable PostgreSQL",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            WorkspaceRoot = harness.ActiveProfile.WorkspaceRootPath,
            PostgresHost = "db.internal",
            PostgresPort = 5432,
            PostgresDatabaseName = "candoitall",
            PostgresUsername = "postgres",
            PostgresPassword = "component-secret"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ComponentTestHarness> CreateUnlockedHarnessAsync() => ComponentTestHarness.CreateAsync(options: new TestHarnessOptions {
        ConfigurationOverrides = new Dictionary<string, string?> {
            ["Database:Provider"] = null,
            ["Database:ConnectionString"] = null
        }
    });

    private sealed class AllowedAccess : IApiTokenAdministrationAccess {
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
}

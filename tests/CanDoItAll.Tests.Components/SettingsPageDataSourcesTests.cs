using Bunit;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsPageDataSourcesTests
{
    [Fact]
    public async Task Settings_page_renders_data_sources_tab_with_saved_profiles_and_editor_actions()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        var databaseProfiles = harness.Context.Services.GetRequiredService<DatabaseProfileWorkspaceService>();

        var postgresSave = await databaseProfiles.SaveProfileAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Component PostgreSQL",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            WorkspaceRoot = harness.ActiveProfile.WorkspaceRootPath,
            PostgresHost = "db.internal",
            PostgresPort = 5432,
            PostgresDatabaseName = "candoitall",
            PostgresUsername = "postgres",
            PostgresPassword = "component-secret"
        });

        Assert.True(postgresSave.IsSuccess);

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current runtime selection", cut.Markup);
            Assert.Contains("Component PostgreSQL", cut.Markup);
            Assert.Contains("database-profile-new-managed", cut.Markup);
            Assert.Contains("database-profile-new-external", cut.Markup);
            Assert.Contains("database-profile-new-postgres", cut.Markup);
            Assert.Contains("database-snapshot-source-summary", cut.Markup);
            Assert.Contains("database-clone-create", cut.Markup);
            Assert.Contains("database-snapshot-local-create", cut.Markup);
            Assert.Contains("database-snapshot-ipfs-create", cut.Markup);
        });

        cut.Find("[data-testid='database-profile-row-" + postgresSave.Value.ToString("N") + "']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-profile-postgres-host", cut.Markup);
            Assert.Contains("database-profile-test-connection", cut.Markup);
            Assert.Contains("database-profile-create-empty", cut.Markup);
            Assert.Contains("database-profile-activate", cut.Markup);
            Assert.Contains("database-profile-delete", cut.Markup);
        });
    }

    [Fact]
    public async Task Settings_page_shows_schema_alert_and_applies_current_schema_for_outdated_profile()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        var databaseProfiles = harness.Context.Services.GetRequiredService<DatabaseProfileWorkspaceService>();
        var databasePath = Path.Combine(harness.RootPath, "external", "outdated.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var outdatedSave = await databaseProfiles.SaveProfileAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Outdated SQLite",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
            SqliteDatabasePath = databasePath,
            WorkspaceRoot = Path.GetDirectoryName(databasePath)
        });
        Assert.True(outdatedSave.IsSuccess);

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.RenderComponent<SettingsPage>();
        cut.Find("[data-testid='database-profile-row-" + outdatedSave.Value.ToString("N") + "']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-profile-schema-alert", cut.Markup);
            Assert.Contains("database-profile-apply-schema", cut.Markup);
            Assert.Contains("Needs schema", cut.Markup);
        });

        cut.Find("[data-testid='database-profile-activate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Apply the current schema before activating this data source.", cut.Markup);
            Assert.Contains("database-profile-schema-alert", cut.Markup);
        });

        cut.Find("[data-testid='database-profile-apply-schema']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current database schema applied.", cut.Markup);
            Assert.Contains("Schema current", cut.Markup);
            Assert.DoesNotContain("database-profile-schema-alert", cut.Markup);
        });
    }

    [Fact]
    public async Task Transfer_dialog_blocks_preview_and_offers_schema_apply_for_outdated_target()
    {
        await using var harness = await CreateUnlockedHarnessAsync();
        var databaseProfiles = harness.Context.Services.GetRequiredService<DatabaseProfileWorkspaceService>();
        var databasePath = Path.Combine(harness.RootPath, "external", "transfer-target.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var targetSave = await databaseProfiles.SaveProfileAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Transfer target SQLite",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
            SqliteDatabasePath = databasePath,
            WorkspaceRoot = Path.GetDirectoryName(databasePath)
        });
        Assert.True(targetSave.IsSuccess);

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.RenderComponent<SettingsPage>();
        cut.Find("[data-testid='database-profile-row-" + targetSave.Value.ToString("N") + "']").Click();
        cut.WaitForElement("[data-testid='database-profile-transfer-settings']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-transfer-target-schema-alert", cut.Markup);
            Assert.Contains("database-transfer-target-apply-schema", cut.Markup);
            Assert.DoesNotContain("database-transfer-items", cut.Markup);
            Assert.DoesNotContain("No settings groups are available", cut.Markup);
        });
    }

    [Fact]
    public async Task Settings_page_surfaces_locked_data_sources_mode()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-data-sources-locked-message", cut.Markup);
            Assert.Contains("Configured SQLite override", cut.Markup);
        });

        Assert.True(cut.Find("[data-testid='database-profile-new-managed']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='database-profile-save']").HasAttribute("disabled"));
    }

    private static async Task<ComponentTestHarness> CreateUnlockedHarnessAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-settings-tests");
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

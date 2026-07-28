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

        var cut = harness.Context.Render<SettingsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current runtime selection", cut.Markup);
            Assert.Contains("Component PostgreSQL", cut.Markup);
            Assert.Contains("database-profile-new-postgres", cut.Markup);
            Assert.DoesNotContain("database-profile-new-managed", cut.Markup);
            Assert.DoesNotContain("database-profile-new-external", cut.Markup);
            Assert.DoesNotContain(LegacyProviderName(), cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("database-clone-create", cut.Markup);
            Assert.DoesNotContain("database-snapshot-deferred", cut.Markup);
            Assert.DoesNotContain("database-snapshot-local-create", cut.Markup);
            Assert.DoesNotContain("database-snapshot-ipfs-create", cut.Markup);
        });

        cut.Find("[data-testid='database-profile-row-" + postgresSave.Value.ToString("N") + "']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-profile-postgres-host", cut.Markup);
            Assert.Contains("database-profile-test-connection", cut.Markup);
            Assert.Contains("database-profile-create-empty", cut.Markup);
            Assert.Contains("database-profile-activate", cut.Markup);
            Assert.Contains("Activate for restart", cut.Markup);
            Assert.Contains("database-profile-delete", cut.Markup);
        });
    }

    [Fact]
    public async Task Settings_page_omits_deferred_database_snapshot_actions()
    {
        await using var harness = await CreateUnlockedHarnessAsync();

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.Render<SettingsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("database-snapshot-deferred", cut.Markup);
            Assert.DoesNotContain("database-snapshot-local-create", cut.Markup);
            Assert.DoesNotContain("database-snapshot-ipfs-restore", cut.Markup);
        });
    }

    [Fact]
    public async Task Settings_page_surfaces_locked_data_sources_mode()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=data-sources");

        var cut = harness.Context.Render<SettingsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("database-data-sources-locked-message", cut.Markup);
            Assert.Contains("Configured PostgreSQL override", cut.Markup);
        });

        Assert.True(cut.Find("[data-testid='database-profile-new-postgres']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='database-profile-save']").HasAttribute("disabled"));
    }

    private static async Task<ComponentTestHarness> CreateUnlockedHarnessAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-settings-tests");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("bootstrap");

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

    private static string LegacyProviderName() => "Sqlite";
}

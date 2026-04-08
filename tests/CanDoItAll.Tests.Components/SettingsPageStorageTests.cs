using Bunit;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsPageStorageTests {
    [Fact]
    public async Task Settings_page_renders_storage_tab_with_saved_targets_and_editor_actions() {
        await using var harness = await CreateUnlockedHarnessAsync();
        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();
        var storageRoot = Path.Combine(harness.ActiveProfile.WorkspaceRootPath, "storage", "project-assets");
        Directory.CreateDirectory(storageRoot);

        var saveResult = await workspaceService.SaveStorageAsync(new StorageCatalogEditorModel {
            Name = "Project assets lane",
            ProviderKind = StorageProviderKind.FileSystem,
            ConnectionMode = StorageConnectionMode.Local,
            EndpointOrRoot = storageRoot,
            DisplayOrder = 20,
            DefaultPurposes = [StorageUsagePurpose.ProjectAsset, StorageUsagePurpose.PromptAttachment]
        });

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=storage");

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.WaitForAssertion(() => {
            Assert.Contains("Storage catalog", cut.Markup);
            Assert.Contains("storage-settings-summary", cut.Markup);
            Assert.Contains("storage-settings-wizard", cut.Markup);
            Assert.Contains("storage-settings-new-filesystem", cut.Markup);
            Assert.Contains("storage-settings-new-ipfs", cut.Markup);
            Assert.Contains("storage-settings-new-ftp", cut.Markup);
            Assert.Contains("Project assets lane", cut.Markup);
        });

        cut.Find("[data-testid='storage-catalog-row-" + saveResult.Value.ToString("N") + "']").Click();

        cut.WaitForAssertion(() => {
            Assert.Equal("Project assets lane", cut.Find("[data-testid='storage-settings-name']").GetAttribute("value"));
            Assert.Contains("storage-settings-test", cut.Markup);
            Assert.Contains("storage-settings-save", cut.Markup);
            Assert.Contains("storage-settings-delete", cut.Markup);
        });

        cut.Find("[data-testid='storage-settings-name']").Change("Project assets lane updated");
        cut.Find("[data-testid='storage-settings-save']").Click();

        cut.WaitForAssertion(() => {
            Assert.Contains("Storage target saved.", cut.Markup);
            Assert.Contains("Project assets lane updated", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Next step", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() => {
            Assert.Equal(storageRoot, cut.Find("[data-testid='storage-settings-endpoint']").GetAttribute("value"));
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Next step", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() => {
            Assert.Contains("storage-settings-purpose-grid", cut.Markup);
            Assert.True(cut.Find("[data-testid='storage-settings-purpose-projectasset']").HasAttribute("checked"));
            Assert.True(cut.Find("[data-testid='storage-settings-purpose-promptattachment']").HasAttribute("checked"));
        });

        cut.Find("[data-testid='storage-settings-test']").Click();

        cut.WaitForAssertion(() => {
            Assert.Contains("Accessible local root", cut.Markup);
            Assert.Contains("Healthy", cut.Markup);
        });
    }

    private static async Task<ComponentTestHarness> CreateUnlockedHarnessAsync() {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-storage-settings-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("bootstrap");

        return await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = new Dictionary<string, string?> {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
                ["Database:Provider"] = null,
                ["Database:ConnectionString"] = null
            }
        });
    }
}

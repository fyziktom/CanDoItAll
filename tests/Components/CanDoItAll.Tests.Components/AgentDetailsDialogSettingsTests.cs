using System.Reflection;
using System.Runtime.CompilerServices;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogSettingsTests
{
    [Fact]
    public void Native_external_workspace_root_survives_save()
    {
        using var context = CreateContext(out var workspaceProxy, out var externalTargetRegistryFactory);
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 5);
        var externalRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"agent-settings-root-{Guid.NewGuid():N}"));

        cut.Find("[data-testid='agents-catalog-workspace-external-roots-input']").Input(externalRoot);
        cut.Find("[data-testid='agents-catalog-workspace-external-roots-add']").Click();

        cut.WaitForAssertion(() =>
        {
            var selectionText = cut
                .Find("[data-testid='agents-catalog-workspace-external-roots-table']")
                .TextContent;
            Assert.Contains(externalRoot, selectionText, StringComparison.Ordinal);
            Assert.Contains("external-target/v1/", selectionText, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            var alias = Assert.Single(saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            var binding = Assert.Single(saved.WorkspaceToolAccess.ExternalTargetRootBindings);
            Assert.StartsWith("external-target/v1/", alias, StringComparison.Ordinal);
            Assert.Contains(binding.RootId, alias, StringComparison.Ordinal);

            var configurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                saved.WorkspaceToolAccess);
            var reloaded = AgentWorkspaceToolAccessMetadata.Read(configurationJson);
            Assert.Equal([alias], reloaded.AllowedExternalTargetAliases);
            Assert.Equal([binding], reloaded.ExternalTargetRootBindings);

            var freshRegistry = externalTargetRegistryFactory.Create([binding]);
            Assert.Equal(
                ExternalTargetAliasResolutionKind.Resolved,
                freshRegistry.TryResolve(alias, out var resolvedPath, out _));
            Assert.Equal(externalRoot, resolvedPath);

            var selectionText = cut
                .Find("[data-testid='agents-catalog-workspace-external-roots-table']")
                .TextContent;
            Assert.Contains(externalRoot, selectionText, StringComparison.Ordinal);
            Assert.Contains(alias, selectionText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Legacy_external_workspace_root_is_migrated_with_a_binding_on_save()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var context = CreateContext(out var workspaceProxy, out var externalTargetRegistryFactory);
        const string legacyAlias = "external-target/C/repositories/legacy-agent-root";
        var editor = CreateEditor();
        editor.WorkspaceToolAccess.AllowedExternalTargetAliases = [legacyAlias];
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            var canonicalAlias = Assert.Single(
                saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            var binding = Assert.Single(saved.WorkspaceToolAccess.ExternalTargetRootBindings);
            Assert.StartsWith("external-target/v1/", canonicalAlias, StringComparison.Ordinal);
            Assert.DoesNotContain(
                legacyAlias,
                saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            Assert.Contains(binding.RootId, canonicalAlias, StringComparison.Ordinal);

            var configurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                saved.WorkspaceToolAccess);
            Assert.Contains(canonicalAlias, configurationJson, StringComparison.Ordinal);
            Assert.DoesNotContain(legacyAlias, configurationJson, StringComparison.Ordinal);

            var freshRegistry = externalTargetRegistryFactory.Create([binding]);
            Assert.Equal(
                ExternalTargetAliasResolutionKind.Resolved,
                freshRegistry.TryResolve(canonicalAlias, out var resolvedPath, out _));
            Assert.Equal(Path.GetFullPath(@"C:\repositories\legacy-agent-root"), resolvedPath);
        });
    }

    [Theory]
    [InlineData(AgentWorkspaceToolProfileKind.SoftwareDevelopment)]
    [InlineData(AgentWorkspaceToolProfileKind.Custom)]
    public void Changing_workspace_profile_preserves_existing_external_root_binding(
        AgentWorkspaceToolProfileKind selectedProfile)
    {
        using var context = CreateContext(out var workspaceProxy, out var externalTargetRegistryFactory);
        var persistedRegistry = externalTargetRegistryFactory.Create([]);
        var externalRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"agent-settings-existing-root-{Guid.NewGuid():N}"));
        Assert.True(persistedRegistry.TryCreateAlias(externalRoot, out var alias));
        var binding = Assert.Single(persistedRegistry.ExportBindings([alias]));
        var editor = CreateEditor();
        editor.WorkspaceToolAccess = AgentWorkspaceToolAccessProfiles.CreateSettings(
            AgentWorkspaceToolProfileKind.ReadOnly);
        editor.WorkspaceToolAccess.AllowedExternalTargetAliases = [alias];
        editor.WorkspaceToolAccess.ExternalTargetRootBindings = [binding];
        var storageCatalogId = Guid.NewGuid();
        editor.WorkspaceToolAccess.AllowAllStorageCatalogs = true;
        editor.WorkspaceToolAccess.AllowedStorageCatalogIds = [storageCatalogId];
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        cut.Find("[data-testid='agents-catalog-workspace-profile']")
            .Change(selectedProfile.ToString());
        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.Equal([alias], saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            Assert.Equal([binding], saved.WorkspaceToolAccess.ExternalTargetRootBindings);
            Assert.True(saved.WorkspaceToolAccess.AllowAllStorageCatalogs);
            Assert.Equal([storageCatalogId], saved.WorkspaceToolAccess.AllowedStorageCatalogIds);
        });
    }

    [Fact]
    public void Relative_external_workspace_root_is_rejected_without_clearing_the_input()
    {
        using var context = CreateContext(out _, out _);
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 5);
        const string relativeRoot = "relative-workspace-root";

        cut.Find("[data-testid='agents-catalog-workspace-external-roots-input']")
            .Input(relativeRoot);
        cut.Find("[data-testid='agents-catalog-workspace-external-roots-add']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                relativeRoot,
                cut.Find("[data-testid='agents-catalog-workspace-external-roots-input']")
                    .GetAttribute("value"));
            var validation = cut.Find(
                "[data-testid='agents-catalog-workspace-external-roots-validation']");
            Assert.Contains("absolute path", validation.TextContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Storage_catalog_picker_selection_is_saved_from_the_typed_field()
    {
        var archiveCatalog = CreateStorageCatalog("Release archive", "D:\\storage\\releases");
        var workingCatalog = CreateStorageCatalog("Working files", "D:\\storage\\working");
        var catalogSource = new RecordingStorageCatalogSelectionSource(
            [archiveCatalog, workingCatalog]);
        using var context = CreateContext(out var workspaceProxy, out _, catalogSource);
        var dialogHost = context.Render<DialogHost>();
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        var openTask = cut
            .Find("[data-testid='agents-catalog-storage-selection-choose']")
            .ClickAsync(new MouseEventArgs());

        dialogHost.WaitForElement(
            $"[data-testid='agents-catalog-storage-selection-dialog-option-{archiveCatalog.Id:N}']");
        Assert.Equal(1, catalogSource.ListCalls);

        dialogHost
            .Find("[data-testid='agents-catalog-storage-selection-dialog-picker-search']")
            .Input("Release archive");
        Assert.Single(dialogHost.FindAll(
            "[data-testid^='agents-catalog-storage-selection-dialog-option-']:not([data-testid$='-shell'])"));

        dialogHost
            .Find($"[data-testid='agents-catalog-storage-selection-dialog-option-{archiveCatalog.Id:N}']")
            .Click();
        dialogHost
            .Find("[data-testid='agents-catalog-storage-selection-dialog-apply']")
            .Click();
        await openTask.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            var selected = cut.Find(
                $"[data-testid='agents-catalog-storage-selection-selected-row-{archiveCatalog.Id:N}']");
            Assert.Contains(archiveCatalog.Name, selected.TextContent, StringComparison.Ordinal);
            Assert.Contains(archiveCatalog.Id.ToString("D"), selected.TextContent, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.Equal([archiveCatalog.Id], saved.WorkspaceToolAccess.AllowedStorageCatalogIds);
        });
    }

    [Fact]
    public void Removing_a_saved_external_root_removes_its_alias_and_binding_on_save()
    {
        using var context = CreateContext(out var workspaceProxy, out var externalTargetRegistryFactory);
        var registry = externalTargetRegistryFactory.Create([]);
        var externalRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"agent-settings-remove-root-{Guid.NewGuid():N}"));
        Assert.True(registry.TryCreateAlias(externalRoot, out var alias));
        var binding = Assert.Single(registry.ExportBindings([alias]));
        var editor = CreateEditor();
        editor.WorkspaceToolAccess.AllowedExternalTargetAliases = [alias];
        editor.WorkspaceToolAccess.ExternalTargetRootBindings = [binding];
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        cut.Find("[data-testid='agents-catalog-workspace-external-roots-table'] button")
            .Click();
        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.Empty(saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            Assert.Empty(saved.WorkspaceToolAccess.ExternalTargetRootBindings);
        });
    }

    [Fact]
    public void Removing_a_saved_storage_catalog_removes_its_id_on_save()
    {
        var catalog = CreateStorageCatalog("Retired archive", "D:\\storage\\retired");
        var catalogSource = new RecordingStorageCatalogSelectionSource([catalog]);
        using var context = CreateContext(out var workspaceProxy, out _, catalogSource);
        var editor = CreateEditor();
        editor.WorkspaceToolAccess.AllowedStorageCatalogIds = [catalog.Id];
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        cut.WaitForElement(
                $"[data-testid='agents-catalog-storage-selection-selected-row-{catalog.Id:N}-remove']")
            .Click();
        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.Empty(saved.WorkspaceToolAccess.AllowedStorageCatalogIds);
        });
    }

    [Fact]
    public void External_call_approval_switch_updates_the_runtime_policy()
    {
        using var context = CreateContext();
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 1);

        Assert.True(editor.Permissions.RequiresApprovalForExternalCalls);

        cut.Find("[data-testid='agents-catalog-require-external-approval']").Change(false);

        Assert.False(editor.Permissions.RequiresApprovalForExternalCalls);
    }

    [Fact]
    public async Task Cancelling_auto_approval_confirmation_leaves_the_policy_disabled()
    {
        using var context = CreateContext();
        var dialogHost = context.Render<DialogHost>();
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 1);

        var changeTask = cut
            .Find("[data-testid='agents-catalog-auto-approval']")
            .ChangeAsync(true);

        dialogHost.WaitForElement("[data-testid='agents-auto-approval-confirmation']");
        Assert.False(editor.Permissions.AutoApproveExternalCallsByDefault);

        dialogHost.Find("[data-testid='agents-auto-approval-cancel']").Click();
        await changeTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(editor.Permissions.AutoApproveExternalCallsByDefault);
        Assert.False(cut
            .Find("[data-testid='agents-catalog-auto-approval']")
            .HasAttribute("checked"));
        Assert.Empty(cut.FindAll("[data-testid='agents-catalog-auto-approval-warning']"));
        Assert.Empty(context.Services.GetRequiredService<DialogService>().Dialogs);
    }

    [Fact]
    public async Task Acknowledging_auto_approval_enables_the_policy_and_keeps_a_warning_visible()
    {
        using var context = CreateContext();
        var dialogHost = context.Render<DialogHost>();
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 1);

        var changeTask = cut
            .Find("[data-testid='agents-catalog-auto-approval']")
            .ChangeAsync(true);

        dialogHost.WaitForElement("[data-testid='agents-auto-approval-confirmation']");
        var confirmButton = dialogHost.Find("[data-testid='agents-auto-approval-confirm']");
        Assert.True(confirmButton.HasAttribute("disabled"));
        Assert.False(editor.Permissions.AutoApproveExternalCallsByDefault);

        dialogHost
            .Find("[data-testid='agents-auto-approval-risk-acknowledgement']")
            .Change(true);

        confirmButton = dialogHost.Find("[data-testid='agents-auto-approval-confirm']");
        Assert.False(confirmButton.HasAttribute("disabled"));
        confirmButton.Click();
        await changeTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(editor.Permissions.AutoApproveExternalCallsByDefault);
        Assert.True(cut
            .Find("[data-testid='agents-catalog-auto-approval']")
            .HasAttribute("checked"));
        var warning = cut.Find("[data-testid='agents-catalog-auto-approval-warning']");
        Assert.Contains("Automatic approval is enabled", warning.TextContent, StringComparison.Ordinal);
        Assert.Empty(context.Services.GetRequiredService<DialogService>().Dialogs);
    }

    [Fact]
    public void Identity_contains_tags_without_a_separate_tags_tab()
    {
        using var context = CreateContext();
        var editor = CreateEditor();
        editor.Tags = ["architecture", "review"];
        var cut = RenderTab(context, editor, selectedTabIndex: 0);

        var selectedTab = cut.Find("button[role='tab'][aria-selected='true']");
        Assert.Equal("Identity", selectedTab.TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid='agents-catalog-tags-section']"));
        Assert.Single(cut.FindAll("[data-testid='agents-catalog-tags-input']"));

        var tabLabels = cut.FindAll("button[role='tab']")
            .Select(tab => tab.TextContent.Trim())
            .ToList();
        Assert.DoesNotContain("Tags", tabLabels);
    }

    private static BunitContext CreateContext()
        => CreateContext(out _, out _);

    private static BunitContext CreateContext(
        out RecordingWorkspaceServiceProxy workspaceProxy,
        out IExternalTargetPathRegistryFactory externalTargetRegistryFactory,
        IStorageCatalogSelectionSource? storageCatalogSource = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddStubProviderRuntimeAdministration();

        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        externalTargetRegistryFactory = new ExternalTargetPathRegistryFactory();
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton(externalTargetRegistryFactory);
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(
            storageCatalogSource ?? new RecordingStorageCatalogSelectionSource([]));
        context.Services.AddSingleton(
            (ProjectsService)RuntimeHelpers.GetUninitializedObject(typeof(ProjectsService)));
        context.Services.AddSingleton(
            (SecretService)RuntimeHelpers.GetUninitializedObject(typeof(SecretService)));
        var avatarGenerationService = new AgentAvatarGenerationService(
            new UnavailableAgentImageGenerationService(),
            NullLogger<AgentAvatarGenerationService>.Instance);
        context.Services.AddSingleton(avatarGenerationService);
        context.Services.AddSingleton<IAvatarGenerationGateway>(
            new AgentAvatarGenerationGateway(workspaceService, avatarGenerationService));
        return context;
    }

    private static IRenderedComponent<TestAgentDetailsDialog> RenderTab(
        BunitContext context,
        AgentEditorModel editor,
        int selectedTabIndex)
    {
        return context.Render<TestAgentDetailsDialog>(parameters => parameters
            .Add(component => component.TestEditor, editor)
            .Add(component => component.TestSelectedTabIndex, selectedTabIndex));
    }

    private static AgentEditorModel CreateEditor()
    {
        return new AgentEditorModel
        {
            Name = "Runtime policy test agent",
            Permissions = AgentPermissionsPolicy.Default with
            {
                AutoApproveExternalCallsByDefault = false
            }
        };
    }

    private static StorageCatalogSummary CreateStorageCatalog(string name, string endpoint)
    {
        return new StorageCatalogSummary(
            Guid.NewGuid(),
            name,
            StorageProviderKind.FileSystem,
            StorageConnectionMode.Local,
            endpoint,
            DisplayOrder: 0,
            IsEnabled: true,
            IsSystemDefault: false,
            IsReadOnly: false,
            StorageCapability.Read | StorageCapability.Write,
            StorageHealthStatus.Healthy,
            LastTestedAtUtc: null,
            LastHealthMessage: string.Empty);
    }

    public sealed class TestAgentDetailsDialog : AgentDetailsDialog
    {
        [Parameter]
        public AgentEditorModel TestEditor { get; set; } = new();

        [Parameter]
        public int TestSelectedTabIndex { get; set; }

        protected override Task OnInitializedAsync()
        {
            SetBaseField("editorModel", TestEditor);
            SetBaseField("tagValues", TestEditor.Tags);
            SetBaseField("selectedTabIndex", TestSelectedTabIndex);
            SetBaseField("isLoading", false);
            return Task.CompletedTask;
        }

        private void SetBaseField(string fieldName, object value)
        {
            var field = typeof(AgentDetailsDialog).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(AgentDetailsDialog).FullName, fieldName);
            field.SetValue(this, value);
        }
    }

    private sealed class RecordingStorageCatalogSelectionSource(
        IReadOnlyList<StorageCatalogSummary> catalogs)
        : IStorageCatalogSelectionSource
    {
        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<StorageCatalogSummary>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(catalogs);
        }
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public List<AgentEditorModel> SavedModels { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) =>
                    SaveAgent((AgentEditorModel)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult<IReadOnlyList<AgentDefinition>>([]),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        private Task<Guid> SaveAgent(AgentEditorModel model)
        {
            SavedModels.Add(model);
            return Task.FromResult(model.Id ?? Guid.NewGuid());
        }
    }
}

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
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogSettingsTests
{
    [Fact]
    public void Native_external_workspace_root_survives_save()
    {
        using var context = CreateContext(out var workspaceProxy, out var externalTargetRegistry);
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 5);
        var externalRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"agent-settings-root-{Guid.NewGuid():N}"));

        cut.Find("[data-testid='agents-catalog-workspace-external-roots']").Change(externalRoot);
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

            Assert.Equal(
                ExternalTargetAliasResolutionKind.Resolved,
                externalTargetRegistry.TryResolve(alias, out var resolvedPath, out _));
            Assert.Equal(externalRoot, resolvedPath);

            Assert.Equal(
                alias,
                cut.Find("[data-testid='agents-catalog-workspace-external-roots']")
                    .GetAttribute("value"));
        });
    }

    [Theory]
    [InlineData(AgentWorkspaceToolProfileKind.SoftwareDevelopment)]
    [InlineData(AgentWorkspaceToolProfileKind.Custom)]
    public void Changing_workspace_profile_preserves_existing_external_root_binding(
        AgentWorkspaceToolProfileKind selectedProfile)
    {
        using var context = CreateContext(out var workspaceProxy, out _);
        var persistedRegistry = new ExternalTargetPathRegistry();
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
        var cut = RenderTab(context, editor, selectedTabIndex: 5);

        cut.Find("[data-testid='agents-catalog-workspace-profile']")
            .Change(selectedProfile.ToString());
        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.Equal([alias], saved.WorkspaceToolAccess.AllowedExternalTargetAliases);
            Assert.Equal([binding], saved.WorkspaceToolAccess.ExternalTargetRootBindings);
        });
    }

    [Fact]
    public void Relative_external_workspace_root_is_rejected_without_clearing_the_editor()
    {
        using var context = CreateContext(out var workspaceProxy, out _);
        var editor = CreateEditor();
        var cut = RenderTab(context, editor, selectedTabIndex: 5);
        const string relativeRoot = "relative-workspace-root";

        cut.Find("[data-testid='agents-catalog-workspace-external-roots']")
            .Change(relativeRoot);
        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(workspaceProxy.SavedModels);
            Assert.Equal(
                relativeRoot,
                cut.Find("[data-testid='agents-catalog-workspace-external-roots']")
                    .GetAttribute("value"));
            var notification = Assert.Single(
                context.Services.GetRequiredService<NotificationService>().Messages);
            Assert.Equal(NotificationSeverity.Error, notification.Severity);
            Assert.Equal("Agent save failed", notification.Summary);
            Assert.Contains("absolute paths", notification.Detail, StringComparison.Ordinal);
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
        out IExternalTargetPathRegistry externalTargetRegistry)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();

        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        externalTargetRegistry = new ExternalTargetPathRegistry();
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton(externalTargetRegistry);
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
            SetBaseField(
                "externalWorkspaceRootsText",
                string.Join(Environment.NewLine, TestEditor.WorkspaceToolAccess.AllowedExternalTargetAliases));
            SetBaseField(
                "allowedStorageCatalogIdsText",
                string.Join(
                    Environment.NewLine,
                    TestEditor.WorkspaceToolAccess.AllowedStorageCatalogIds.Select(item => item.ToString("D"))));
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

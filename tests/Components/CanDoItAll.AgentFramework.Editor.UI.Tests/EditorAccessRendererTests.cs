using Bunit;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Editor.UiSandbox;
using CanDoItAll.AgentFramework.Editor.UiSandbox.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentEditorUi;

public sealed class EditorAccessRendererTests {
    [Theory]
    [InlineData(AgentEditorScenario.Representative)]
    [InlineData(AgentEditorScenario.VerificationReadFailure)]
    [InlineData(AgentEditorScenario.VerificationUnknown)]
    public async Task Fixture_verification_preserves_dirty_context_and_read_only_recovery(AgentEditorScenario scenario) {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>(p => p.Add(x => x.Scenario, scenario));
        await Tab(cut, AgentEditorSection.Capabilities);
        await cut.FindAll("[data-testid='agents-details-capability-toggle']")[0].ClickAsync();
        await cut.Find("form").SubmitAsync();
        var edit = cut.FindComponent<EditForm>().Instance.EditContext;
        await Tab(cut, AgentEditorSection.Identity);
        cut.Find("[data-testid='agents-catalog-name']").Input("Unsaved before Verify 東京");
        await Tab(cut, AgentEditorSection.Capabilities);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Hold diagnostic").ClickAsync();
        var verify = cut.FindAll("[data-testid='agents-details-capability-verify']")[0].ClickAsync();
        await Tab(cut, AgentEditorSection.Identity);
        cut.Find("[data-testid='agents-catalog-instructions']").Input("Typed during held diagnostic");
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Release diagnostic").ClickAsync();
        await verify;
        if (scenario != AgentEditorScenario.Representative) {
            Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
            await cut.Find("[data-testid='agents-editor-retry-verification']").ClickAsync();
        }
        Assert.Equal(scenario == AgentEditorScenario.VerificationUnknown,
            cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Unsaved before Verify 東京", ((AgentEditorModel)edit!.Model).Name);
        Assert.Contains("Synthetic diagnostics: 1", cut.Markup);
        Assert.Contains("Synthetic writes: 1", cut.Markup);
        Assert.Contains("Read back: Fixture agent", cut.Markup);
    }

    [Fact]
    public async Task All_six_sections_edit_the_same_draft_and_saved_fixture_is_read_back() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        var edit = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)edit.Model;
        await Tab(cut, AgentEditorSection.Memory);
        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Input("team");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("team-fixture");
        await cut.Find("[data-testid='agents-catalog-memory-add-binding']").ClickAsync();
        Assert.Equal("team", Assert.Single(draft.MemoryAccess.ProviderBindings).Alias.Value);
        await Tab(cut, AgentEditorSection.ProjectStructureAccess);
        Assert.Empty(cut.FindAll("[data-testid='agents-catalog-project-structure-projects']"));
        await cut.Find("[data-testid='agents-catalog-project-structure-load']").ClickAsync();
        cut.Find("[data-testid='agents-catalog-project-structure-projects'] input").Change(true);
        cut.Find("[data-testid='agents-catalog-project-structure-task-write']").Change(true);
        Assert.Single(draft.ProjectStructureAccess.AllowedProjectIds);
        Assert.True(draft.ProjectStructureAccess.CanWriteTasks);
        await Tab(cut, AgentEditorSection.WorkspaceTools);
        cut.Find("[data-testid='agents-catalog-workspace-write']").Change(true);
        cut.Find("[data-testid='agents-catalog-storage-read']").Change(true);
        Assert.True(draft.WorkspaceToolAccess.CanReadFiles);
        Assert.True(draft.WorkspaceToolAccess.CanReadStorage);
        await Tab(cut, AgentEditorSection.Secrets);
        cut.Find("[data-testid='agents-catalog-secret-list'] input").Change(true);
        Assert.Single(draft.AllowedSecretReferences);
        await Tab(cut, AgentEditorSection.ProcessAccess);
        cut.Find("[data-testid='agents-catalog-process-write']").Change(true);
        Assert.True(draft.ProcessAccess.CanRead);
        Assert.Contains("selection is unavailable", cut.Markup);
        await Tab(cut, AgentEditorSection.Capabilities);
        await cut.FindAll("[data-testid='agents-details-capability-toggle']")[0].ClickAsync();
        Assert.Single(draft.SelectedCapabilityIds);
        Assert.Contains("writes: 0", cut.Markup);
        Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.Find("form").SubmitAsync();
        Assert.Contains("Read back: Fixture agent", cut.Markup);
        Assert.Contains("writes: 1", cut.Markup);
    }

    [Fact]
    public async Task Unadded_memory_and_root_candidates_survive_tabs_and_never_become_saved_permissions() {
        using var context = Context();
        var cut = context.Render<ScenarioEditor>();
        var draft = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        await Tab(cut, AgentEditorSection.Memory);
        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Input("Žluťoučký 東京 unadded");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("team-fixture");
        await Tab(cut, AgentEditorSection.WorkspaceTools);
        cut.Find("[data-testid='agents-catalog-workspace-external-roots-input']").Input("unadded/東京");
        await Tab(cut, AgentEditorSection.Identity);
        await cut.Find("form").SubmitAsync();
        Assert.Empty(draft.MemoryAccess.ProviderBindings);
        Assert.Empty(draft.WorkspaceToolAccess.AllowedExternalTargetAliases);
        await Tab(cut, AgentEditorSection.Memory);
        Assert.Equal("Žluťoučký 東京 unadded", cut.Find("[data-testid='agents-catalog-memory-new-alias']").GetAttribute("value"));
        await Tab(cut, AgentEditorSection.WorkspaceTools);
        Assert.Equal("unadded/東京", cut.Find("[data-testid='agents-catalog-workspace-external-roots-input']").GetAttribute("value"));
        await cut.Find("[data-testid='agents-catalog-workspace-external-roots-add']").ClickAsync();
        Assert.Contains("native absolute path", cut.Markup);
        Assert.Contains("writes: 1", cut.Markup);
    }

    [Fact]
    public async Task Missing_references_and_secret_purpose_survive_partial_lists_and_select_all() {
        using var context = Context();
        var missing = Guid.NewGuid();
        var visible = Guid.NewGuid();
        var secret = new AgentEditorSecret(Guid.NewGuid(), "Safe name", "Token");
        var draft = new AgentEditorModel { Name = "Retain references" };
        draft.ProjectStructureAccess.AllowedProjectIds = [missing];
        draft.AllowedSecretReferences.Add(new(secret.Id, secret.Name, "existing-purpose"));
        var state = new AgentEditorAccessState(new(Guid.NewGuid()), draft) {
            Projects = [new(visible, "Visible project")], ProjectsLoaded = true, ProjectsRequested = true,
            SecretsError = "Unauthorized metadata read"
        };
        state.Changed = EventCallback.Factory.Create<AgentEditorAccessIntent>(this, state.Apply);
        var projects = context.Render<AgentEditorProjectStructureAccessSection>(p => p.Add(x => x.State, state));
        await projects.FindAll("button").Single(button => button.TextContent.Trim() == "Select all").ClickAsync();
        Assert.Equal(2, draft.ProjectStructureAccess.AllowedProjectIds.Count);
        Assert.False(draft.ProjectStructureAccess.AllowAllProjects);
        Assert.Contains(missing.ToString(), projects.Markup);
        var secrets = context.Render<AgentEditorSecretsSection>(p => p.Add(x => x.State, state));
        Assert.Contains("existing-purpose", secrets.Markup);
        state.Apply(new AgentEditorAccessIntent.Secret(secret, true));
        Assert.Equal("existing-purpose", Assert.Single(draft.AllowedSecretReferences).Purpose);
        await secrets.FindAll("button").Single(button => button.TextContent.Trim() == "Remove selection").ClickAsync();
        Assert.Empty(draft.AllowedSecretReferences);
    }

    [Fact]
    public async Task Memory_removal_uses_real_rows_and_cleans_only_the_removed_provider() {
        using var context = Context();
        var first = MemoryProviderInstanceId.Parse("first");
        var second = MemoryProviderInstanceId.Parse("second");
        var settings = new AgentMemoryAccessSettings {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            ProviderBindings = [new(AgentMemoryProviderAlias.Parse("one"), first), new(AgentMemoryProviderAlias.Parse("two"), second)],
            PreferredProviderInstanceId = first, DefaultProviderInstanceId = first,
            AllowedProviderInstanceIds = [first, second]
        };
        var state = new AgentMemoryEditorState(settings);
        var cut = context.Render<AgentMemorySection>(p => p.Add(x => x.State, state));
        await cut.Find("[data-testid='agents-catalog-memory-remove-one']").ClickAsync();
        Assert.Equal(second, Assert.Single(settings.ProviderBindings).ProviderInstanceId);
        Assert.Null(settings.PreferredProviderInstanceId);
        Assert.Null(settings.DefaultProviderInstanceId);
        Assert.Equal(second, Assert.Single(settings.AllowedProviderInstanceIds));
        cut.Find("[data-testid='agents-catalog-memory-mode']").Change(nameof(AgentMemoryInvocationMode.ExplicitDirective));
        Assert.False(settings.CanUseMemoryTools);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Risk_confirmation_requires_acknowledgement_and_returns_explicit_decision(bool accept) {
        using var context = Context();
        bool? result = null;
        var cut = context.Render<AgentWorkspaceRiskConfirmation>(p => p
            .Add(x => x.Question, "Enable scripts?")
            .Add(x => x.Completed, value => result = value));
        Assert.True(cut.Find("[data-testid='agents-workspace-risk-confirm']").HasAttribute("disabled"));
        if (accept) {
            cut.Find("[data-testid='agents-workspace-risk-risk-acknowledgement']").Change(true);
        }
        await cut.Find($"[data-testid='agents-workspace-risk-{(accept ? "confirm" : "cancel")}']").ClickAsync();
        Assert.Equal(accept, result);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IStorageCatalogSelectionSource, EditorStorageSource>();
        return context;
    }
    private static Task Tab(IRenderedComponent<ScenarioEditor> cut, AgentEditorSection section)
        => cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(section)].ClickAsync();
}

using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWorkspaceShellTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Global_shell_renders_projection_tabs_and_command_strip()
    {
        using var context = CreateContext(out var client);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-shell']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Global, client.LastRequest?.Scope.Kind);
        Assert.Contains("Definitions", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Launch plans", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Live runs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("History", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Blazor app delivery", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Store pending", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='processes-command-strip']"));
    }

    [Fact]
    public void Project_shell_passes_project_scope_and_selection_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var processId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var cut = context.RenderComponent<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProcessIdQuery, processId)
            .Add(component => component.RunIdQuery, runId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-tab-panel-liveruns']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Project, client.LastRequest?.Scope.Kind);
        Assert.Equal(projectId, client.LastRequest?.Scope.ProjectId);
        Assert.Equal(processId, client.LastRequest?.Selection.ProcessId);
        Assert.Equal(runId, client.LastRequest?.Selection.RunId);
    }

    [Fact]
    public void Refresh_button_requests_forced_projection_refresh()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-refresh']")));
        cut.Find("[data-testid='processes-refresh']").Click();

        cut.WaitForAssertion(() => Assert.True(client.Requests.Last().ForceRefresh));
        Assert.Contains("Refresh requested", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_search_passes_query_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-search']")));
        cut.Find("[data-testid='processes-definition-search']").Input("architecture");
        cut.Find("[data-testid='processes-definition-search-submit']").Click();

        cut.WaitForAssertion(() => Assert.Equal("architecture", client.Requests.Last().DefinitionCatalogQuery.SearchText));
        Assert.Contains("Architecture decision governance", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_scope_filter_passes_scope_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-scope-project']")));
        cut.Find("[data-testid='processes-definition-scope-project']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCatalogScopeKind.Project, client.Requests.Last().DefinitionCatalogQuery.ScopeFilter));
        Assert.Contains("No definitions match the current search", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Feed_defaults_button_uses_application_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-feed-defaults']")));
        cut.Find("[data-testid='processes-feed-defaults']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, client.FeedDefaultsCommandCount));
        Assert.Contains("default process definition", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Refresh token", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projection_service_rejects_mismatched_scope_state()
    {
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(clock),
            new ProcessDefinitionEditorProjectionService(clock),
            new ProcessDefinitionRoleEditorProjectionService(clock),
            new ProcessDefinitionCanvasEditorProjectionService(clock),
            new ProcessDefinitionStepEditorProjectionService(clock),
            new ProcessTemplateCatalogProjectionService(clock));
        var selection = new ProcessWorkspaceSelectionProjection(
            ProcessId: null,
            RunId: null,
            LaunchPlanId: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Project, ProjectId: null),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Global, Guid.Parse("55555555-5555-5555-5555-555555555555")),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false)));
    }

    [Fact]
    public void Agent_context_button_uses_projected_context_key()
    {
        using var context = CreateContext(out _);
        var runId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var cut = context.RenderComponent<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.RunIdQuery, runId));
        var navigation = context.Services.GetRequiredService<NavigationManager>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-agent-context']")));
        cut.Find("[data-testid='processes-agent-context']").Click();

        Assert.Contains("/agents?processContext=", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString($"processes:workspace:run:{runId:N}"), navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_editor_renders_authoring_sections_from_projection()
    {
        using var context = CreateContext(out _);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-editor']")));
        Assert.Contains("Identity", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Governance", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Contracts", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Simulation", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Blazor app delivery", cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='processes-definition-editor-manager-override']"));
    }

    [Fact]
    public void Definition_save_uses_typed_editor_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-save']")));
        cut.Find("[data-testid='processes-definition-editor-owner']").Input("Architecture owner");
        cut.Find("[data-testid='processes-definition-editor-manager-override']").Input("Use the architecture board manager.");
        cut.Find("[data-testid='processes-definition-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionEditorCommandKind.SaveDraft, client.LastEditorCommand?.CommandKind));
        Assert.Contains("Draft saved", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Architecture owner", client.LastEditorCommand?.Draft.Identity.OwnerName);
        Assert.Equal("Use the architecture board manager.", client.LastEditorCommand?.Draft.Governance.ManagerOverrideSummary);
        Assert.NotNull(cut.Find("[data-testid='processes-definition-role-editor']"));
    }

    [Fact]
    public void Definition_publish_shows_blocking_lint_errors()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-publish']")));
        cut.Find("[data-testid='processes-definition-editor-name']").Input(string.Empty);
        cut.Find("[data-testid='processes-definition-publish']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionEditorCommandKind.Publish, client.LastEditorCommand?.CommandKind));
        Assert.Contains("Definition name is required", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Rejected", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Role_editor_renders_roles_templates_and_step_bindings()
    {
        using var context = CreateContext(out _);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-role-editor']")));
        Assert.Equal("Solution architect", cut.Find("[data-testid='processes-role-display-name']").GetAttribute("value"));
        Assert.Equal("process-role-template/solution-architect", cut.Find("[data-testid='processes-role-template-source']").GetAttribute("value"));
        Assert.Contains("Solution architect template", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Architecture decision", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Approver", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Role_save_uses_typed_role_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-save']")));
        cut.Find("[data-testid='processes-role-display-name']").Input("Principal architecture steward");
        cut.Find("[data-testid='processes-role-executor-kind']").Change(ProcessDefinitionRoleExecutorKind.PersonOrAgent.ToString());
        cut.Find("[data-testid='processes-role-project-assignment']").Change(ProcessDefinitionRoleProjectAssignmentKind.Manager.ToString());
        cut.Find("[data-testid='processes-role-allocation']").Input("75");
        cut.Find("[data-testid='processes-role-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionRoleCommandKind.SaveRole, client.LastRoleCommand?.CommandKind));
        Assert.Equal("Principal architecture steward", client.LastRoleCommand?.Draft.DisplayName);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, client.LastRoleCommand?.Draft.PreferredExecutorKind);
        Assert.Equal(ProcessDefinitionRoleProjectAssignmentKind.Manager, client.LastRoleCommand?.Draft.PreferredProjectAssignmentRole);
        Assert.Equal(75, client.LastRoleCommand?.Draft.DefaultAllocationPercent);
        Assert.Contains("Role saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Role_apply_template_uses_selected_template_action()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-apply-template']")));
        cut.Find("[data-testid='processes-role-template-action']").Change("role-template.solution-architect");
        cut.Find("[data-testid='processes-role-apply-template']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionRoleCommandKind.ApplyTemplate, client.LastRoleCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"), client.LastRoleCommand?.TemplateActionKey);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, client.LastRoleCommand?.Draft.OverrideStatus);
        Assert.Contains("Role template applied", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canvas_renders_shared_workbench_nodes_toolbox_selection_and_route_edges()
    {
        using var context = CreateContext(out _);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        var workbench = cut.FindComponent<CanvasWorkbench>();
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "step:architecture-decision");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "branch:architecture-decision");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "role:solution-architect");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "artifact:architecture-decision:adr");
        Assert.Contains(workbench.Instance.Surface.Links, link =>
            string.Equals(link.Kind, ProcessDefinitionCanvasEdgeKind.BranchRoute.ToString(), StringComparison.Ordinal) &&
            link.SourceId == "step:architecture-decision" &&
            link.TargetId == "branch:architecture-decision");
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox-window']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-step-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-branch-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-role-solution-architect']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-artifact-architecture-decision-adr']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-edge-branch-route-architecture-decision-router']"));

        await cut.InvokeAsync(() => workbench.Instance.OnSelectionChanged("artifact:architecture-decision:adr", "[\"artifact:architecture-decision:adr\"]", 1));

        cut.WaitForAssertion(() => Assert.Contains("Architecture decision record", cut.Find("[data-testid='processes-canvas-selection']").TextContent, StringComparison.Ordinal));
        Assert.Contains("Artifact", cut.Find("[data-testid='processes-canvas-selection']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Canvas_toolbox_action_uses_typed_canvas_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox-process-step-implementation']")));
        cut.Find("[data-testid='processes-canvas-node-step-architecture-decision']").Click();
        cut.Find("[data-testid='processes-canvas-toolbox-process-step-implementation']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.AddStep, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"), client.LastCanvasCommand?.ToolboxActionKey);
        Assert.Equal(new ProcessDefinitionCanvasNodeKey("step:architecture-decision"), client.LastCanvasCommand?.SelectedNodeKey);
        Assert.Contains("Canvas command accepted", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Canvas_recompose_uses_typed_canvas_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-canvas-recompose']")));
        cut.Find("[data-testid='processes-canvas-recompose']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.Recompose, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(ProcessDefinitionCanvasRecompositionMode.BalancedFlow, client.LastCanvasCommand?.RecompositionMode);
        Assert.Contains("Canvas recomposed", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='processes-definition-role-editor']"));
    }

    [Fact]
    public void Step_editor_renders_operation_routes_artifacts_and_subprocess_mapping()
    {
        using var context = CreateContext(out _);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-step-editor']")));
        Assert.Equal("Architecture decision", cut.Find("[data-testid='processes-step-title']").GetAttribute("value"));
        Assert.Equal(ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination.ToString(), cut.Find("[data-testid='processes-step-operation-target-scope']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-operation-writeexternalartifactdestination']"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-branch-approved']"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-artifact-architecture-decision-record']"));
        Assert.Contains("Delivery default", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_save_uses_typed_step_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-step-save']")));
        cut.Find("[data-testid='processes-step-title']").Input("Architecture decision checkpoint");
        cut.Find("[data-testid='processes-step-operation-target-scope']").Change(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetReadOnly.ToString());
        cut.Find("[data-testid='processes-step-operation-readprojectstructure']").Change(true);
        cut.Find("[data-testid='processes-step-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.SaveStep, client.LastStepCommand?.CommandKind));
        var command = Assert.IsType<ProcessDefinitionStepEditorCommand>(client.LastStepCommand);
        Assert.Equal("Architecture decision checkpoint", command.Draft.Basic.Title);
        Assert.Equal(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetReadOnly, command.Draft.OperationContract.TargetScope);
        Assert.Contains(ProcessDefinitionStepOperationKind.ReadProjectStructure, command.Draft.OperationContract.AllowedOperations);
        Assert.Contains("Step saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_route_artifact_and_subprocess_commands_use_typed_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-step-add-branch-outcome']")));
        cut.Find("[data-testid='processes-step-add-branch-outcome']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.AddBranchOutcome, client.LastStepCommand?.CommandKind));
        Assert.Contains("Route added", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-step-add-artifact-expectation']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.AddArtifactExpectation, client.LastStepCommand?.CommandKind));
        Assert.Contains("Artifact added", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-step-kind']").Change(ProcessDefinitionStepKind.Subprocess.ToString());
        cut.Find("[data-testid='processes-step-subprocess-definition']").Change("delivery-default");
        cut.Find("[data-testid='processes-step-map-subprocess']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.MapSubprocess, client.LastStepCommand?.CommandKind));
        Assert.Equal(ProcessDefinitionStepKind.Subprocess, client.LastStepCommand?.Draft.Basic.StepKind);
        Assert.Equal("delivery-default", client.LastStepCommand?.Draft.SubprocessMapping.ProcessKey);
    }

    [Fact]
    public void Template_library_renders_search_categories_and_preview_tabs()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library']")));
        Assert.Contains("Template catalog is projected from canonical JSON", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Blazor app delivery", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='processes-template-library-search']").Input("architect");
        cut.Find("[data-testid='processes-template-library-search-submit']").Click();

        cut.WaitForAssertion(() => Assert.Equal("architect", client.Requests.Last().TemplateCatalogQuery.SearchText));
        cut.Find("[data-testid='processes-template-library-category-roles']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogCategoryKind.Roles, client.Requests.Last().TemplateCatalogQuery.Category));
        Assert.Contains("Solution architect", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-preview-tab-json']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogPreviewTabKind.Json, client.Requests.Last().TemplateCatalogQuery.PreviewTab));
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-json']"));
        cut.Find("[data-testid='processes-template-library-preview-tab-structure']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogPreviewTabKind.Structure, client.Requests.Last().TemplateCatalogQuery.PreviewTab));
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-structure']"));
    }

    [Fact]
    public void Template_library_imports_role_and_artifact_components_with_target_step()
    {
        using var context = CreateContext(out var client);
        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library-import-process']")));
        cut.Find("[data-testid='processes-template-library-import-process']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportProcess, client.LastTemplateImportCommand?.CommandKind));
        Assert.Contains("Process template imported", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-import-role-role-blazor-app-delivery-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportRole, client.LastTemplateImportCommand?.CommandKind));
        Assert.Equal(new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"), client.LastTemplateImportCommand?.ItemKey);
        Assert.Contains("Role component imported", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-artifact-target']").Change("architecture-decision");
        cut.Find("[data-testid='processes-template-library-import-artifact-artifact-blazor-app-delivery-architecture-decision-architecture-decision-record']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportArtifact, client.LastTemplateImportCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionStepKey("architecture-decision"), client.LastTemplateImportCommand?.TargetStepKey);
        Assert.Contains("Artifact component imported", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Processes_navigation_contributor_adds_processes_to_shell_navigation()
    {
        var items = ShellNavigation.GetItems(0, [new ProcessesShellNavigationContributor()]);
        var processes = Assert.Single(items, item => item.Route == "/processes");
        var liveProcesses = Assert.Single(items, item => item.Route == "/processes/live");

        Assert.Equal("Processes", processes.Title);
        Assert.Equal("account_tree", processes.Icon);
        Assert.Equal("Live Processes", liveProcesses.Title);
        Assert.Equal("monitor_heart", liveProcesses.Icon);
    }

    [Fact]
    public void Live_processes_dashboard_uses_own_projection_page()
    {
        using var context = CreateContext(out var client);

        var cut = context.RenderComponent<LiveProcessesDashboard>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-dashboard']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Global, client.LastRequest?.Scope.Kind);
        Assert.NotNull(cut.Find("[data-testid='live-processes-page']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-command-strip']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-tabs']"));
    }

    private static TestContext CreateContext(out RecordingProcessWorkspaceProjectionClient client)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        client = new RecordingProcessWorkspaceProjectionClient();
        context.Services.AddSingleton<IProcessWorkspaceProjectionClient>(client);
        return context;
    }

    private sealed class RecordingProcessWorkspaceProjectionClient : IProcessWorkspaceProjectionClient
    {
        public List<ProcessWorkspaceShellRequest> Requests { get; } = [];

        public ProcessWorkspaceShellRequest? LastRequest => Requests.LastOrDefault();

        public int FeedDefaultsCommandCount { get; private set; }

        public ProcessDefinitionEditorCommand? LastEditorCommand { get; private set; }

        public ProcessDefinitionRoleEditorCommand? LastRoleCommand { get; private set; }

        public ProcessDefinitionCanvasCommand? LastCanvasCommand { get; private set; }

        public ProcessDefinitionStepEditorCommand? LastStepCommand { get; private set; }

        public ProcessTemplateImportCommand? LastTemplateImportCommand { get; private set; }

        public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
            ProcessWorkspaceShellRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            await Task.Yield();
            return CreateShell(request, lastReceipt: null);
        }

        public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
            ProcessDefinitionFeedDefaultsCommand command,
            CancellationToken cancellationToken = default)
        {
            FeedDefaultsCommandCount++;
            return Task.FromResult(new ProcessDefinitionCatalogCommandReceipt(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProcessDefinitionCatalogCommandKind.FeedDefaults,
                ProcessDefinitionCatalogCommandStatus.Accepted,
                new ProcessDefinitionCatalogRefreshToken("feed-defaults:test"),
                AffectedDefinitionCount: 2,
                Now,
                "2 default process definition(s) are available from template pack test."));
        }

        public Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
            ProcessDefinitionEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastEditorCommand = command;
            var lint = CreateEditorLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionEditorCommandStatus.Rejected
                : ProcessDefinitionEditorCommandStatus.Accepted;
            var authoringStatus = command.CommandKind == ProcessDefinitionEditorCommandKind.Publish && status == ProcessDefinitionEditorCommandStatus.Accepted
                ? ProcessDefinitionAuthoringStatus.Published
                : ProcessDefinitionAuthoringStatus.Draft;
            var versionToken = new ProcessDefinitionEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionEditorCommandReceipt(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionEditorCommandStatus.Accepted
                    ? command.CommandKind == ProcessDefinitionEditorCommandKind.Publish
                        ? "Definition published."
                        : "Draft saved."
                    : "Definition was not published because blocking lint issues remain.",
                lint.Issues);
            var projection = CreateEditor(command.Draft.DefinitionKey, command.Draft, authoringStatus, versionToken, lint, receipt);
            return Task.FromResult(new ProcessDefinitionEditorCommandResult(receipt, projection));
        }

        public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
            ProcessDefinitionRoleEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastRoleCommand = command;
            var lint = CreateRoleLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionRoleCommandStatus.Rejected
                : ProcessDefinitionRoleCommandStatus.Accepted;
            var versionToken = new ProcessDefinitionRoleEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionRoleCommandReceipt(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionRoleCommandStatus.Accepted
                    ? command.CommandKind == ProcessDefinitionRoleCommandKind.ApplyTemplate
                        ? "Role template applied."
                        : "Role saved."
                    : "Role was not saved because blocking role lint issues remain.",
                lint.Issues);
            var projection = CreateRoleEditor(command.DefinitionKey, command.Draft, versionToken, lint, receipt);
            return Task.FromResult(new ProcessDefinitionRoleEditorCommandResult(receipt, projection));
        }

        public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
            ProcessDefinitionCanvasCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCanvasCommand = command;
            var versionToken = new ProcessDefinitionCanvasVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionCanvasCommandReceipt(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                command.CommandKind,
                ProcessDefinitionCanvasCommandStatus.Accepted,
                versionToken,
                Now,
                command.CommandKind == ProcessDefinitionCanvasCommandKind.Recompose
                    ? "Canvas recomposed."
                    : "Canvas command accepted.");
            var projection = CreateCanvas(command.DefinitionKey, versionToken, receipt, command.CommandKind);
            return Task.FromResult(new ProcessDefinitionCanvasCommandResult(receipt, projection));
        }

        public Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
            ProcessDefinitionStepEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastStepCommand = command;
            var lint = CreateStepLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionStepCommandStatus.Rejected
                : ProcessDefinitionStepCommandStatus.Accepted;
            var versionToken = new ProcessDefinitionStepEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionStepCommandReceipt(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionStepCommandStatus.Accepted
                    ? command.CommandKind switch
                    {
                        ProcessDefinitionStepCommandKind.AddBranchOutcome => "Route added.",
                        ProcessDefinitionStepCommandKind.AddArtifactExpectation => "Artifact added.",
                        ProcessDefinitionStepCommandKind.MapSubprocess => "Subprocess mapped.",
                        _ => "Step saved."
                    }
                    : "Step command rejected.",
                lint.Issues);
            var projection = CreateStepEditor(command.DefinitionKey, command.Draft, versionToken, lint, receipt, command.CommandKind);
            return Task.FromResult(new ProcessDefinitionStepEditorCommandResult(receipt, projection));
        }

        public Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
            ProcessTemplateImportCommand command,
            CancellationToken cancellationToken = default)
        {
            LastTemplateImportCommand = command;
            var versionToken = new ProcessTemplateCatalogVersionToken("templates:test:1");
            var receipt = new ProcessTemplateImportCommandReceipt(
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                command.CommandKind,
                ProcessTemplateImportCommandStatus.Accepted,
                versionToken,
                Now,
                command.CommandKind switch
                {
                    ProcessTemplateImportCommandKind.ImportRole => "Role component imported.",
                    ProcessTemplateImportCommandKind.ImportArtifact => "Artifact component imported.",
                    _ => "Process template imported."
                });
            var imported = new[]
            {
                new ProcessTemplateImportedComponentProjection(
                    command.ItemKey,
                    command.CommandKind switch
                    {
                        ProcessTemplateImportCommandKind.ImportRole => ProcessTemplateCatalogItemKind.Role,
                        ProcessTemplateImportCommandKind.ImportArtifact => ProcessTemplateCatalogItemKind.Artifact,
                        _ => ProcessTemplateCatalogItemKind.Process
                    },
                    command.ItemKey.Value,
                    "blazor-app-delivery",
                    command.ItemKey.Value,
                    "sha256:component-test",
                    command.TargetStepKey,
                    Now)
            };
            var projection = CreateTemplateCatalog(command.TargetDefinitionKey, command.Query, receipt, imported);
            return Task.FromResult(new ProcessTemplateImportCommandResult(receipt, projection));
        }

        private static ProcessWorkspaceShellProjection CreateShell(
            ProcessWorkspaceShellRequest request,
            ProcessDefinitionCatalogCommandReceipt? lastReceipt)
        {
            var catalog = CreateDefinitionCatalog(request.DefinitionCatalogQuery, request.TemplateCatalogQuery, lastReceipt);
            var authorization = new ProcessWorkspaceAuthorizationProjection(
                CanReadDefinitions: true,
                CanRefreshProjections: true,
                CanOpenAgentContext: true,
                CanEditDefinitions: false,
                CanLaunchRuns: false);

            return new ProcessWorkspaceShellProjection(
                request.Scope,
                request.Selection,
                request.Scope.Kind == ProcessWorkspaceScopeKind.Project ? "Project processes" : "Processes",
                "Projection-first process workspace.",
                catalog,
                new ProcessLiveRunSummaryProjection(0, 0, 0, null, "Runtime projection snapshots are not available in this workspace shell."),
                new ProcessWorkspaceProjectionRefreshProjection(
                    request.ForceRefresh
                        ? ProcessWorkspaceProjectionStatus.RefreshRequested
                        : ProcessWorkspaceProjectionStatus.ProjectionStoreUnavailable,
                    Now,
                    SourceGlobalSequence: 0,
                    BacklogEventCount: 0,
                    request.ForceRefresh
                        ? "Projection refresh was requested through the application boundary."
                        : "Projection store integration is pending; runtime data is intentionally not read by the UI shell."),
                authorization,
                CreateTabs(),
                CreateCommands(),
                CreateAgentEntry(request));
        }

        private static ProcessDefinitionCatalogProjection CreateDefinitionCatalog(
            ProcessDefinitionCatalogQueryProjection query,
            ProcessTemplateCatalogQueryProjection templateQuery,
            ProcessDefinitionCatalogCommandReceipt? lastReceipt)
        {
            var items = new[]
            {
                new ProcessDefinitionCatalogItemProjection(
                    new ProcessDefinitionCatalogItemKey("blazor-app-delivery"),
                    ProcessDefinitionCatalogScopeKind.Global,
                    "Blazor app delivery",
                    "Build and prove a Blazor application.",
                    ProcessDefinitionCatalogItemStatus.TemplateDefault,
                    "High",
                    "GovernedLive",
                    Now,
                    CompatibilityIssueCount: 0),
                new ProcessDefinitionCatalogItemProjection(
                    new ProcessDefinitionCatalogItemKey("architecture-decision-governance"),
                    ProcessDefinitionCatalogScopeKind.Global,
                    "Architecture decision governance",
                    "Review and approve architecture decisions.",
                    ProcessDefinitionCatalogItemStatus.TemplateDefault,
                    "Medium",
                    "Assisted",
                    Now,
                    CompatibilityIssueCount: 0)
            };
            ProcessDefinitionCatalogItemProjection[] scopeFiltered = query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project
                ? []
                : items;
            var filtered = string.IsNullOrWhiteSpace(query.SearchText)
                ? scopeFiltered
                : scopeFiltered
                    .Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.Key.Value.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            var selected = query.SelectedDefinitionKey is { } selectedKey
                ? filtered.FirstOrDefault(item => item.Key == selectedKey)
                : filtered.FirstOrDefault();

            return new ProcessDefinitionCatalogProjection(
                PublishedDefinitionCount: items.Length,
                DraftDefinitionCount: 0,
                TemplateCompatibilityIssueCount: 0,
                string.IsNullOrWhiteSpace(query.SearchText)
                    ? "2 default definition(s) loaded from template pack test."
                    : $"{filtered.Length} definition(s) match '{query.SearchText}'.",
                query.SearchText ?? string.Empty,
                selected?.Key,
                [
                    new(ProcessDefinitionCatalogScopeKind.All, "All definitions", "All visible definitions.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.All),
                    new(ProcessDefinitionCatalogScopeKind.Global, "Global defaults", "Template-backed defaults.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Global),
                    new(ProcessDefinitionCatalogScopeKind.Project, "Project", "Project-specific definitions.", Count: 0, IsSelected: query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project)
                ],
                filtered,
                selected,
                selected is null ? null : CreateEditor(selected.Key, templateQuery),
                lastReceipt);
        }

        private static ProcessDefinitionEditorProjection CreateEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessTemplateCatalogQueryProjection? templateQuery = null)
        {
            var draft = new ProcessDefinitionEditorDraftProjection(
                key,
                new ProcessDefinitionEditorIdentityProjection(
                    key.Value == "blazor-app-delivery" ? "Blazor app delivery" : "Architecture decision governance",
                    "Global",
                    "Delivery requester",
                    "Delivery owner",
                    "Build and prove the process.",
                    "Deliver a useful process."),
                new ProcessDefinitionEditorGovernanceProjection(
                    ProcessDefinitionCriticalityLevel.High,
                    ProcessDefinitionAutonomyLevel.Guarded,
                    ProcessDefinitionOperatingModeKind.GovernedLive,
                    ProcessDefinitionAuthoringStatus.TemplateDefault,
                    "Manager override.",
                    "Governance notes.",
                    "Change summary.",
                    "Governance policy."),
                new ProcessDefinitionEditorContractProjection(
                    "Interface contract.",
                    "Constitution rule.",
                    "Operating mode summary."),
                new ProcessDefinitionEditorSimulationProjection(
                    "Safe deterministic simulation.",
                    StepCount: 5,
                    RequiredRoleCount: 2,
                    RequiredArtifactExpectationCount: 3,
                    IsReadyForSimulation: true));

            return CreateEditor(
                key,
                draft,
                ProcessDefinitionAuthoringStatus.TemplateDefault,
                new ProcessDefinitionEditorVersionToken($"template:{key.Value}"),
                new ProcessDefinitionEditorLintProjection([]),
                lastReceipt: null) with
            {
                TemplateCatalog = CreateTemplateCatalog(
                    key,
                    templateQuery ?? new ProcessTemplateCatalogQueryProjection(
                        SearchText: null,
                        ProcessTemplateCatalogCategoryKind.All,
                        SelectedItemKey: null,
                        ProcessTemplateCatalogPreviewTabKind.Overview,
                        Take: 50),
                    lastReceipt: null,
                    importedComponents: [])
            };
        }

        private static ProcessDefinitionEditorProjection CreateEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionEditorDraftProjection draft,
            ProcessDefinitionAuthoringStatus status,
            ProcessDefinitionEditorVersionToken versionToken,
            ProcessDefinitionEditorLintProjection lint,
            ProcessDefinitionEditorCommandReceipt? lastReceipt)
            => new(
                key,
                versionToken,
                status,
                draft.Identity,
                draft.Governance with { WorkingStatus = status },
                draft.Contracts,
                draft.Simulation,
                lint,
                [
                    new(ProcessDefinitionEditorCommandKind.SaveDraft, "Save draft", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Publish, "Publish", "publish", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Archive, "Archive", "archive", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Delete, "Delete", "delete", IsEnabled: true, DisabledReason: null)
                ],
                lastReceipt)
            {
                RoleEditor = CreateRoleEditor(key),
                Canvas = CreateCanvas(key),
                StepEditor = CreateStepEditor(key)
            };

        private static ProcessDefinitionRoleEditorProjection CreateRoleEditor(ProcessDefinitionCatalogItemKey key)
        {
            var draft = CreateRoleDraft();
            return CreateRoleEditor(
                key,
                draft,
                new ProcessDefinitionRoleEditorVersionToken($"template:{key.Value}:roles"),
                new ProcessDefinitionRoleLintProjection([]),
                lastReceipt: null);
        }

        private static ProcessDefinitionRoleEditorProjection CreateRoleEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionRoleDraftProjection draft,
            ProcessDefinitionRoleEditorVersionToken versionToken,
            ProcessDefinitionRoleLintProjection lint,
            ProcessDefinitionRoleCommandReceipt? lastReceipt)
        {
            var role = new ProcessDefinitionRoleProjection(
                draft.RoleKey,
                draft.DisplayName,
                draft.SnapshotSummary,
                draft,
                StepBindingCount: 1);
            return new ProcessDefinitionRoleEditorProjection(
                key,
                versionToken,
                role.RoleKey,
                [role],
                role,
                [
                    new ProcessDefinitionRoleTemplateActionProjection(
                        new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"),
                        "Solution architect template",
                        "Owns architecture decisions and technical tradeoffs.",
                        new ProcessDefinitionRoleKey("solution-architect"),
                        "solution-architect",
                        "Solution architect next",
                        ProcessDefinitionRoleExecutorKind.PersonOrAgent,
                        DefaultAllocationPercent: 60)
                ],
                [
                    new ProcessDefinitionStepRoleBindingProjection(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        draft.RoleKey,
                        draft.DisplayName,
                        ProcessStepRoleResponsibilityKind.Approver,
                        IsRequired: true,
                        FallbackOrder: 1,
                        "Rebind to the architecture board when the primary owner is unavailable.")
                ],
                lint,
                [
                    new(ProcessDefinitionRoleCommandKind.AddRole, "Add role", "add", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.SaveRole, "Save role", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.ApplyTemplate, "Apply template", "content_copy", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.DeleteRole, "Delete role", "delete", IsEnabled: true, DisabledReason: null)
                ],
                lastReceipt);
        }

        private static ProcessDefinitionRoleDraftProjection CreateRoleDraft()
            => new(
                new ProcessDefinitionRoleKey("solution-architect"),
                "Solution architect",
                "Own architecture decisions and technical tradeoffs.",
                "Assign a senior architecture owner before launch planning.",
                ProcessDefinitionRoleExecutorKind.PersonOrAgent,
                new ProcessDefinitionWorkflowPreferenceProjection(
                    ProcessDefinitionRoleWorkflowPreferenceKind.AnyActiveWorkflow,
                    WorkflowDefinitionId: null,
                    WorkflowVersionId: null,
                    "Any active workflow"),
                ProcessDefinitionRoleProjectAssignmentKind.Architect,
                IsRequired: true,
                AllowsFallback: true,
                RequiresExplicitApproval: true,
                DefaultAllocationPercent: 60,
                "process-role-template/solution-architect",
                "Solution architect v1",
                "Architecture role template snapshot.",
                ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
                "Resolved from process-role-template/solution-architect.");

        private static ProcessDefinitionStepEditorProjection CreateStepEditor(ProcessDefinitionCatalogItemKey key)
            => CreateStepEditor(
                key,
                CreateStepDraft(),
                new ProcessDefinitionStepEditorVersionToken($"template:{key.Value}:steps"),
                new ProcessDefinitionStepLintProjection([]),
                lastReceipt: null,
                commandKind: null);

        private static ProcessDefinitionStepEditorProjection CreateStepEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionStepDraftProjection draft,
            ProcessDefinitionStepEditorVersionToken versionToken,
            ProcessDefinitionStepLintProjection lint,
            ProcessDefinitionStepCommandReceipt? lastReceipt,
            ProcessDefinitionStepCommandKind? commandKind)
        {
            var projectedDraft = commandKind switch
            {
                ProcessDefinitionStepCommandKind.AddBranchOutcome => draft with
                {
                    BranchOutcomes =
                    [
                        .. draft.BranchOutcomes,
                        new ProcessDefinitionBranchOutcomeProjection(
                            new ProcessDefinitionBranchOutcomeKey("architecture-decision-route-2"),
                            "Route 2",
                            "Second typed route.",
                            new ProcessDefinitionRouteTargetProjection(
                                ProcessDefinitionRouteTargetKind.NextStep,
                                StepKey: null,
                                ArtifactExpectationKey: null,
                                "Next step"),
                            IsBackwardRoute: false,
                            new ProcessDefinitionLoopBudgetProjection(
                                IsRequired: false,
                                MaximumRepeats: 0,
                                FingerprintPolicyKey: string.Empty,
                                ProcessDefinitionRouteTargetKind.Escalate))
                    ]
                },
                ProcessDefinitionStepCommandKind.AddArtifactExpectation => draft with
                {
                    ArtifactExpectations =
                    [
                        .. draft.ArtifactExpectations,
                        new ProcessDefinitionArtifactExpectationProjection(
                            new ProcessDefinitionArtifactExpectationKey("architecture-decision-evidence"),
                            "architecture-decision-evidence",
                            "Architecture decision evidence",
                            ProcessDefinitionArtifactKind.Evidence,
                            IsRequired: true,
                            ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                            ProcessDefinitionArtifactSensitivityLevel.Internal,
                            RetentionDays: 365,
                            WorkflowOutputId: string.Empty,
                            WorkflowOutputName: string.Empty,
                            ProcessDefinitionWorkflowOutputKind.Unspecified,
                            SubprocessChildArtifactExpectationId: null,
                            SubprocessChildStepKey: string.Empty,
                            SubprocessChildArtifactTitle: string.Empty,
                            AllowedFutureUsageSummary: "Reusable for route replay.",
                            ValidationRequirementSummary: "Must identify evidence source.")
                    ]
                },
                _ => draft
            };

            return new ProcessDefinitionStepEditorProjection(
                key,
                versionToken,
                projectedDraft.Basic.StepKey,
                [
                    new ProcessDefinitionStepListItemProjection(
                        projectedDraft.Basic.StepKey,
                        projectedDraft.Basic.Title,
                        projectedDraft.Basic.Subtitle,
                        projectedDraft.Basic.StepKind,
                        Order: 0,
                        IsSelected: true)
                ],
                [projectedDraft],
                projectedDraft,
                [
                    new ProcessDefinitionSubprocessOptionProjection(
                        new ProcessDefinitionCatalogItemKey("delivery-default"),
                        "Delivery default",
                        "Default delivery subprocess.")
                ],
                [
                    new(ProcessDefinitionStepCommandKind.SaveStep, "Save step", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.AddBranchOutcome, "Add route", "alt_route", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.AddArtifactExpectation, "Add artifact", "inventory_2", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.MapSubprocess, "Map subprocess", "account_tree", IsEnabled: true, DisabledReason: null)
                ],
                lint,
                lastReceipt);
        }

        private static ProcessTemplateCatalogProjection CreateTemplateCatalog(
            ProcessDefinitionCatalogItemKey definitionKey,
            ProcessTemplateCatalogQueryProjection query,
            ProcessTemplateImportCommandReceipt? lastReceipt,
            IReadOnlyList<ProcessTemplateImportedComponentProjection> importedComponents)
        {
            var allItems = new[]
            {
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("process:blazor-app-delivery"),
                    ProcessTemplateCatalogItemKind.Process,
                    "Blazor app delivery",
                    "Build and prove a Blazor application.",
                    "blazor-app-delivery",
                    "blazor-app-delivery",
                    "Process",
                    [new("Source", "blazor-app-delivery")],
                    IsSelected: false),
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                    ProcessTemplateCatalogItemKind.Role,
                    "Solution architect",
                    "Owns architecture decisions and technical tradeoffs.",
                    "blazor-app-delivery",
                    "solution-architect",
                    "Role",
                    [new("Executor", "person-or-agent")],
                    IsSelected: false),
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                    ProcessTemplateCatalogItemKind.Artifact,
                    "Architecture decision record",
                    "Must include selected option and rationale.",
                    "blazor-app-delivery",
                    "architecture-decision-record",
                    "Artifact",
                    [new("Artifact", "Deliverable")],
                    IsSelected: false)
            };
            var categoryFiltered = query.Category switch
            {
                ProcessTemplateCatalogCategoryKind.Processes => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Process),
                ProcessTemplateCatalogCategoryKind.Roles => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Role),
                ProcessTemplateCatalogCategoryKind.Artifacts => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Artifact),
                _ => allItems
            };
            var filtered = string.IsNullOrWhiteSpace(query.SearchText)
                ? categoryFiltered.ToArray()
                : categoryFiltered
                    .Where(item => item.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.SourceComponentKey.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            var selected = query.SelectedItemKey is { } selectedKey
                ? filtered.FirstOrDefault(item => item.Key == selectedKey)
                : filtered.FirstOrDefault();
            var selectedQuery = query with { SelectedItemKey = selected?.Key ?? query.SelectedItemKey };
            var importedKeys = importedComponents.Select(component => component.ItemKey).ToHashSet();
            var projectedItems = filtered
                .Select(item => item with
                {
                    IsSelected = selected?.Key == item.Key,
                    Facts = importedKeys.Contains(item.Key)
                        ? [.. item.Facts, new ProcessTemplateCatalogFactProjection("Import", "Imported")]
                        : item.Facts
                })
                .ToArray();
            var preview = selected is null
                ? null
                : new ProcessTemplateCatalogPreviewProjection(
                    selected.Key,
                    selected.Kind,
                    selected.Title,
                    selected.Summary,
                    "processes/blazor-app-delivery/definition.json",
                    "sha256:test-template-hash",
                    "Generated projections are derived from canonical JSON.",
                    "# Blazor app delivery\n\nGenerated from canonical JSON process template `blazor-app-delivery`.",
                    "flowchart TD\n    process[\"Blazor app delivery\"]\n    step[\"Architecture decision\"]\n    process --> step",
                    "{\"key\":\"blazor-app-delivery\",\"displayName\":\"Blazor app delivery\"}",
                    [
                        new("process:blazor-app-delivery", ParentNodeKey: null, ProcessTemplateStructureNodeKind.Process, "Blazor app delivery", "Build and prove a Blazor application.", Depth: 0),
                        new("process:blazor-app-delivery:steps", "process:blazor-app-delivery", ProcessTemplateStructureNodeKind.Section, "Steps", "1 step", Depth: 1),
                        new("process:blazor-app-delivery:steps:architecture-decision", "process:blazor-app-delivery:steps", ProcessTemplateStructureNodeKind.Step, "Architecture decision", "Governed review step.", Depth: 2)
                    ],
                    [
                        new(
                            new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                            ProcessTemplateCatalogItemKind.Role,
                            "Solution architect",
                            "Owns architecture decisions and technical tradeoffs.",
                            "blazor-app-delivery",
                            "solution-architect",
                            importedKeys.Contains(new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"))),
                        new(
                            new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                            ProcessTemplateCatalogItemKind.Artifact,
                            "Architecture decision record",
                            "Must include selected option and rationale.",
                            "blazor-app-delivery",
                            "architecture-decision-record",
                            importedKeys.Contains(new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record")))
                    ]);

            return new ProcessTemplateCatalogProjection(
                definitionKey,
                lastReceipt?.VersionToken ?? new ProcessTemplateCatalogVersionToken("templates:test:0"),
                selectedQuery,
                string.IsNullOrWhiteSpace(query.SearchText)
                    ? "3 template catalog item(s) from pack test-pack."
                    : $"{filtered.Length} template catalog item(s) match '{query.SearchText}'.",
                "test-pack",
                "Template catalog is projected from canonical JSON.",
                [
                    new(ProcessTemplateCatalogCategoryKind.All, "All", "All template items.", allItems.Length, query.Category == ProcessTemplateCatalogCategoryKind.All),
                    new(ProcessTemplateCatalogCategoryKind.Processes, "Processes", "Process templates.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Processes),
                    new(ProcessTemplateCatalogCategoryKind.Roles, "Roles", "Role components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Roles),
                    new(ProcessTemplateCatalogCategoryKind.Artifacts, "Artifacts", "Artifact components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Artifacts)
                ],
                projectedItems,
                selected,
                preview,
                [
                    new(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        "Governed review step",
                        IsDefaultTarget: true)
                ],
                [
                    new(ProcessTemplateImportCommandKind.ImportProcess, "Import process", "account_tree", selected?.Kind == ProcessTemplateCatalogItemKind.Process, null),
                    new(ProcessTemplateImportCommandKind.ImportRole, "Import role", "badge", selected?.Kind == ProcessTemplateCatalogItemKind.Role, null),
                    new(ProcessTemplateImportCommandKind.ImportArtifact, "Import artifact", "inventory_2", selected?.Kind == ProcessTemplateCatalogItemKind.Artifact, null)
                ],
                importedComponents,
                lastReceipt);
        }

        private static ProcessDefinitionStepDraftProjection CreateStepDraft()
            => new(
                new ProcessDefinitionStepBasicDraftProjection(
                    new ProcessDefinitionStepKey("architecture-decision"),
                    "Architecture decision",
                    "Governed review step",
                    "Choose an architecture route from typed outcomes.",
                    ProcessDefinitionStepKind.Decision,
                    TargetLeadHours: 12,
                    AllowsManualSkip: false,
                    AllowsSafeRefusal: true,
                    RequiresApproval: true,
                    RequiresDecisionRecord: true,
                    new ProcessDefinitionRoleKey("solution-architect")),
                new ProcessDefinitionStepOperationContractProjection(
                    ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination,
                    [
                        ProcessDefinitionStepOperationKind.ReadProcessContext,
                        ProcessDefinitionStepOperationKind.WriteExternalArtifactDestination
                    ]),
                new ProcessDefinitionStepContractsProjection(
                    "Architecture concern and project context.",
                    "Architecture decision record.",
                    "Decision evidence and route rationale.",
                    "Solution architect decides the route.",
                    "Escalate when evidence is contradictory."),
                [
                    new ProcessDefinitionBranchOutcomeProjection(
                        new ProcessDefinitionBranchOutcomeKey("approved"),
                        "Approved",
                        "Route to the approved implementation lane.",
                        new ProcessDefinitionRouteTargetProjection(
                            ProcessDefinitionRouteTargetKind.NextStep,
                            StepKey: null,
                            ArtifactExpectationKey: null,
                            "Next step"),
                        IsBackwardRoute: false,
                        new ProcessDefinitionLoopBudgetProjection(
                            IsRequired: false,
                            MaximumRepeats: 0,
                            FingerprintPolicyKey: string.Empty,
                            ProcessDefinitionRouteTargetKind.Escalate))
                ],
                [
                    new ProcessDefinitionStepRoleBindingProjection(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        new ProcessDefinitionRoleKey("solution-architect"),
                        "Solution architect",
                        ProcessStepRoleResponsibilityKind.Approver,
                        IsRequired: true,
                        FallbackOrder: 1,
                        "Rebind to the architecture board when unavailable.")
                ],
                [
                    new ProcessDefinitionArtifactExpectationProjection(
                        new ProcessDefinitionArtifactExpectationKey("architecture-decision-record"),
                        "architecture-decision-record",
                        "Architecture decision record",
                        ProcessDefinitionArtifactKind.Deliverable,
                        IsRequired: true,
                        ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                        ProcessDefinitionArtifactSensitivityLevel.Internal,
                        RetentionDays: 365,
                        WorkflowOutputId: "adr-output",
                        WorkflowOutputName: "Architecture decision record",
                        ProcessDefinitionWorkflowOutputKind.Artifact,
                        SubprocessChildArtifactExpectationId: null,
                        SubprocessChildStepKey: string.Empty,
                        SubprocessChildArtifactTitle: string.Empty,
                        AllowedFutureUsageSummary: "Reusable for implementation planning.",
                        ValidationRequirementSummary: "Must include selected option and rationale.")
                ],
                new ProcessDefinitionSubprocessMappingProjection(
                    ProcessKey: string.Empty,
                    DefinitionSnapshotName: string.Empty,
                    ChildArtifactMappings: []));

        private static ProcessDefinitionCanvasEditorProjection CreateCanvas(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionCanvasVersionToken? versionToken = null,
            ProcessDefinitionCanvasCommandReceipt? receipt = null,
            ProcessDefinitionCanvasCommandKind? commandKind = null)
        {
            var stepKey = new ProcessDefinitionCanvasNodeKey("step:architecture-decision");
            var branchKey = new ProcessDefinitionCanvasNodeKey("branch:architecture-decision");
            var roleKey = new ProcessDefinitionCanvasNodeKey("role:solution-architect");
            var artifactKey = new ProcessDefinitionCanvasNodeKey("artifact:architecture-decision:adr");
            var nodes = new[]
            {
                CreateCanvasNode(
                    stepKey,
                    ProcessDefinitionCanvasNodeKind.Step,
                    commandKind == ProcessDefinitionCanvasCommandKind.AddStep ? "Implementation" : "Architecture decision",
                    "Governed review step",
                    "Select the architecture decision step without losing editor context.",
                    160,
                    220,
                    "info",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: null,
                    ["Step"]),
                CreateCanvasNode(
                    branchKey,
                    ProcessDefinitionCanvasNodeKind.BranchRouter,
                    "Architecture decision routes",
                    "Typed branch router",
                    "Route labels are display text; the route target stays typed.",
                    420,
                    110,
                    "warning",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: null,
                    ["Branch"]),
                CreateCanvasNode(
                    roleKey,
                    ProcessDefinitionCanvasNodeKind.Role,
                    "Solution architect",
                    "person-or-agent",
                    "Architecture authority for the selected step.",
                    160,
                    40,
                    "success",
                    StepKey: null,
                    RoleKey: new ProcessDefinitionRoleKey("solution-architect"),
                    ArtifactKey: null,
                    ["Required"]),
                CreateCanvasNode(
                    artifactKey,
                    ProcessDefinitionCanvasNodeKind.Artifact,
                    "Architecture decision record",
                    "Deliverable",
                    "Required evidence for the selected step.",
                    160,
                    370,
                    "accent",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: "architecture-decision-record",
                    ["Artifact"])
            };
            var edges = new[]
            {
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("branch-route:architecture-decision:router"),
                    ProcessDefinitionCanvasEdgeKind.BranchRoute,
                    stepKey,
                    branchKey,
                    "approved",
                    "Typed route from architecture decision to the approved lane.",
                    "warning",
                    IsBackwardRoute: false),
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("role-binding:solution-architect:architecture-decision"),
                    ProcessDefinitionCanvasEdgeKind.RoleBinding,
                    roleKey,
                    stepKey,
                    "Approver",
                    "Solution architect approves the architecture decision.",
                    "success",
                    IsBackwardRoute: false),
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("artifact:architecture-decision:adr"),
                    ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
                    stepKey,
                    artifactKey,
                    "evidence",
                    "Architecture decision record is required evidence.",
                    "accent",
                    IsBackwardRoute: false)
            };

            return new ProcessDefinitionCanvasEditorProjection(
                key,
                versionToken ?? new ProcessDefinitionCanvasVersionToken($"template:{key.Value}:canvas"),
                new ProcessDefinitionCanvasViewportProjection(960, 560, "Test canvas bounds."),
                nodes,
                edges,
                [
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
                        ProcessDefinitionCanvasToolboxActionKind.Step,
                        "Implementation",
                        "Add an implementation step.",
                        "add",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-step.decision"),
                        ProcessDefinitionCanvasToolboxActionKind.BranchRouter,
                        "Decision router",
                        "Add a typed branch router to the selected step.",
                        "alt_route",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-role-binding"),
                        ProcessDefinitionCanvasToolboxActionKind.RoleBinding,
                        "Role binding",
                        "Connect the selected step to a role.",
                        "badge",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-artifact-expectation"),
                        ProcessDefinitionCanvasToolboxActionKind.ArtifactExpectation,
                        "Artifact expectation",
                        "Attach required evidence to the selected step.",
                        "inventory_2",
                        IsEnabled: true,
                        DisabledReason: null)
                ],
                new ProcessDefinitionCanvasSelectionProjection(
                    ProcessDefinitionCanvasSelectionKind.Step,
                    stepKey,
                    EdgeKey: null,
                    "Architecture decision",
                    "Select the architecture decision step without losing editor context.",
                    "architecture-decision",
                    ["Step"]),
                [
                    new ProcessDefinitionCanvasCommandProjection(
                        ProcessDefinitionCanvasCommandKind.Recompose,
                        "Recompose",
                        "auto_fix_high",
                        IsEnabled: true,
                        DisabledReason: null)
                ],
                receipt);
        }

        private static ProcessDefinitionCanvasEditorNodeProjection CreateCanvasNode(
            ProcessDefinitionCanvasNodeKey nodeKey,
            ProcessDefinitionCanvasNodeKind kind,
            string title,
            string subtitle,
            string summary,
            double x,
            double y,
            string tone,
            ProcessDefinitionStepKey? StepKey,
            ProcessDefinitionRoleKey? RoleKey,
            string? ArtifactKey,
            IReadOnlyList<string> badges)
            => new(
                nodeKey,
                kind,
                title,
                subtitle,
                summary,
                x,
                y,
                Width: kind == ProcessDefinitionCanvasNodeKind.BranchRouter ? 168 : 220,
                Height: kind == ProcessDefinitionCanvasNodeKind.Artifact ? 72 : 92,
                tone,
                StepKey,
                RoleKey,
                ArtifactKey,
                badges,
                []);

        private static ProcessDefinitionEditorLintProjection CreateEditorLint(
            ProcessDefinitionEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.Identity.Name))
            {
                return new ProcessDefinitionEditorLintProjection([]);
            }

            return new ProcessDefinitionEditorLintProjection(
            [
                new ProcessDefinitionEditorLintIssueProjection(
                    "processes.definition.identity.name-required",
                    ProcessDefinitionEditorLintSeverity.Error,
                    ProcessDefinitionEditorLintSection.Identity,
                    "Definition name is required.",
                    "Enter a stable, user-facing definition name.")
            ]);
        }

        private static ProcessDefinitionRoleLintProjection CreateRoleLint(
            ProcessDefinitionRoleEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.DisplayName) &&
                command.Draft.PreferredExecutorKind != ProcessDefinitionRoleExecutorKind.Unspecified &&
                command.Draft.DefaultAllocationPercent is >= 0 and <= 100)
            {
                return new ProcessDefinitionRoleLintProjection([]);
            }

            return new ProcessDefinitionRoleLintProjection(
            [
                new ProcessDefinitionRoleLintIssueProjection(
                    "processes.definition.role.execution.invalid",
                    ProcessDefinitionRoleLintSeverity.Error,
                    ProcessDefinitionRoleLintSection.Execution,
                "Role execution fields are invalid.",
                "Choose a typed executor kind and bounded allocation.")
            ]);
        }

        private static ProcessDefinitionStepLintProjection CreateStepLint(
            ProcessDefinitionStepEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.Basic.Title) &&
                command.Draft.OperationContract.TargetScope != ProcessDefinitionStepTargetScopeKind.Unspecified)
            {
                return new ProcessDefinitionStepLintProjection([]);
            }

            return new ProcessDefinitionStepLintProjection(
            [
                new ProcessDefinitionStepLintIssueProjection(
                    "processes.definition.step.invalid",
                    ProcessDefinitionStepLintSeverity.Error,
                    ProcessDefinitionStepLintSection.Basic,
                    "Step fields are invalid.",
                    "Enter a title and choose an explicit operation target scope.")
            ]);
        }

        private static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs()
            =>
            [
                new(ProcessWorkspaceTabKey.Definitions, "Definitions", "account_tree", "Definition catalog.", "2", IsEnabled: true),
                new(ProcessWorkspaceTabKey.LaunchPlans, "Launch plans", "rocket_launch", "Launch plans.", "0", IsEnabled: true),
                new(ProcessWorkspaceTabKey.LiveRuns, "Live runs", "monitor_heart", "Live runs.", "0", IsEnabled: true),
                new(ProcessWorkspaceTabKey.History, "History", "history", "History.", "0", IsEnabled: true)
            ];

        private static IReadOnlyList<ProcessWorkspaceCommandProjection> CreateCommands()
            =>
            [
                new(ProcessWorkspaceCommandKind.RefreshProjections, "Refresh", "refresh", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.OpenAgentContext, "Agent context", "smart_toy", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.CreateDefinition, "New definition", "add", IsEnabled: false, "Definition editing is not available in this workspace shell."),
                new(ProcessWorkspaceCommandKind.FeedDefaults, "Feed defaults", "download", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.LaunchRun, "Launch", "rocket_launch", IsEnabled: false, "Runtime launch commands are not available in this workspace shell."),
                new(ProcessWorkspaceCommandKind.OpenLiveDashboard, "Live dashboard", "open_in_new", IsEnabled: true, DisabledReason: null)
            ];

        private static ProcessWorkspaceAgentEntryProjection CreateAgentEntry(ProcessWorkspaceShellRequest request)
        {
            if (request.Selection.RunId is { } runId)
            {
                return new ProcessWorkspaceAgentEntryProjection(
                    ProcessWorkspaceAgentEntryKind.RunContext,
                    IsAvailable: true,
                    "Open run agent context",
                    $"processes:workspace:run:{runId:N}",
                    DisabledReason: null);
            }

            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.WorkspaceContext,
                IsAvailable: true,
                "Open process agent context",
                "processes:workspace",
                DisabledReason: null);
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}

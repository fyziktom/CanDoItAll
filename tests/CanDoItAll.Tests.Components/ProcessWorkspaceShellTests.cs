using Bunit;
using CanDoItAll.Components.BaseLib;
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
            new ProcessDefinitionCanvasEditorProjectionService(clock));
        var selection = new ProcessWorkspaceSelectionProjection(
            ProcessId: null,
            RunId: null,
            LaunchPlanId: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Project, ProjectId: null),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
            ForceRefresh: false)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Global, Guid.Parse("55555555-5555-5555-5555-555555555555")),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
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
    public void Canvas_renders_nodes_toolbox_selection_and_route_edges()
    {
        using var context = CreateContext(out _);

        var cut = context.RenderComponent<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-step-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-branch-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-role-solution-architect']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-artifact-architecture-decision-adr']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-edge-branch-route-architecture-decision-router']"));

        cut.Find("[data-testid='processes-canvas-node-artifact-architecture-decision-adr']").Click();

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
    public void Processes_navigation_contributor_adds_processes_to_shell_navigation()
    {
        var items = ShellNavigation.GetItems(0, [new ProcessesShellNavigationContributor()]);
        var processes = Assert.Single(items, item => item.Route == "/processes");

        Assert.Equal("Processes", processes.Title);
        Assert.Equal("account_tree", processes.Icon);
    }

    private static TestContext CreateContext(out RecordingProcessWorkspaceProjectionClient client)
    {
        var context = new TestContext();
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

        private static ProcessWorkspaceShellProjection CreateShell(
            ProcessWorkspaceShellRequest request,
            ProcessDefinitionCatalogCommandReceipt? lastReceipt)
        {
            var catalog = CreateDefinitionCatalog(request.DefinitionCatalogQuery, lastReceipt);
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
                selected is null ? null : CreateEditor(selected.Key),
                lastReceipt);
        }

        private static ProcessDefinitionEditorProjection CreateEditor(ProcessDefinitionCatalogItemKey key)
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
                lastReceipt: null);
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
                Canvas = CreateCanvas(key)
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

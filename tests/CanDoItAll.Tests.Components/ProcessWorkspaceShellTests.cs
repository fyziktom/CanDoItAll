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
            new ProcessDefinitionCatalogProjectionService(clock));
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
                lastReceipt);
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

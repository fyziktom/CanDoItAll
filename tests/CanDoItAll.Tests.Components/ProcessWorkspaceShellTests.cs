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
    public async Task Projection_service_rejects_mismatched_scope_state()
    {
        var service = new ProcessWorkspaceShellProjectionService(new FixedProcessProjectionClock(Now));
        var selection = new ProcessWorkspaceSelectionProjection(
            ProcessId: null,
            RunId: null,
            LaunchPlanId: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Project, ProjectId: null),
            selection,
            ForceRefresh: false)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Global, Guid.Parse("55555555-5555-5555-5555-555555555555")),
            selection,
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
        private readonly ProcessWorkspaceShellProjectionService service = new(new FixedProcessProjectionClock(Now));

        public List<ProcessWorkspaceShellRequest> Requests { get; } = [];

        public ProcessWorkspaceShellRequest? LastRequest => Requests.LastOrDefault();

        public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
            ProcessWorkspaceShellRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return await service.GetShellAsync(request, cancellationToken);
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}

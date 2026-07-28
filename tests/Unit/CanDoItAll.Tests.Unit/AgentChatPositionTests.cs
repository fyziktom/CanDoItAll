using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentChatPositionTests
{
    [Fact]
    public void Context_surface_defaults_to_fail_closed_access()
    {
        var surface = new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind("projects"),
                new AgentChatContextSourceId("projects")),
            "Projects",
            new AgentChatSurfacePosition(
                "projects",
                "portfolio",
                "projects",
                "/projects"));

        Assert.Equal(AgentChatContextScopeAccessMode.AllowListed, surface.AccessMode);
        Assert.Empty(surface.AgentAccess);
    }

    [Fact]
    public async Task Registry_captures_workspace_and_surface_position_for_one_prompt()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(
            "/projects",
            new AgentChatContextEntityReference("project", projectId.ToString("D"), "Delivery"));
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition(
                "route:projects",
                "Projects",
                "/projects",
                "page",
                projectId,
                "Delivery"),
            AgentChatNavigationIdentity.Create());
        using var scopeLease = registry.ActivateScope(
            surface.ToScope(AgentChatContextScopeId.Create()));

        var snapshot = Assert.IsType<AgentChatContextSnapshot>(
            await registry.CaptureAsync());

        Assert.Equal("route:projects", snapshot.WorkspacePosition?.TabId);
        Assert.Equal("projects", snapshot.Scope.SurfacePosition?.Module);
        Assert.Equal(projectId.ToString("D"), snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);

        var agentId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var invocation = AgentChatContextInvocationFactory.Create(
            snapshot,
            agentId,
            chatSessionId: null,
            "Summarize my current selection.",
            operationId,
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);

        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            invocation.Options.TransientContext);
        Assert.Equal(operationId, invocation.Options.InitialActivityOperationId);
        Assert.Contains("route:projects", transientContext.Content, StringComparison.Ordinal);
        Assert.Contains(projectId.ToString("D"), transientContext.Content, StringComparison.Ordinal);
        Assert.Contains("Delivery", transientContext.Content, StringComparison.Ordinal);
        Assert.Contains(
            AgentChatContextInvocationFactory.WorkspacePositionContributorId,
            invocation.Options.Context?.MetadataJson,
            StringComparison.Ordinal);
        Assert.Contains(
            AgentChatContextInvocationFactory.SurfacePositionContributorId,
            invocation.Options.Context?.MetadataJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Strict_capture_rejects_workspace_and_surface_route_mismatch()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition(
                "route:crm",
                "CRM",
                "/crm-hr/crm",
                "page"),
            AgentChatNavigationIdentity.Create());
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/projects").ToScope(AgentChatContextScopeId.Create()));

        Assert.Null(registry.Capture());
        var exception = await Assert.ThrowsAsync<AgentChatContextPositionMismatchException>(
            async () => await registry.CaptureAsync());

        Assert.Equal("/crm-hr/crm", exception.WorkspaceRoute);
        Assert.Equal("/projects", exception.SurfaceRoute);
    }

    [Fact]
    public async Task Strict_capture_rejects_a_workspace_position_without_an_active_module_scope()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("projects", "Projects", "/projects", "page"),
            AgentChatNavigationIdentity.Create());

        Assert.Null(registry.Capture());
        var exception = await Assert.ThrowsAsync<AgentChatContextPositionUnavailableException>(
            async () => await registry.CaptureAsync());

        Assert.Equal(AgentChatContextPositionUnavailablePart.ModuleScope, exception.UnavailablePart);
        Assert.Equal("/projects", exception.KnownRoute);
    }

    [Fact]
    public async Task Strict_capture_rejects_a_surface_without_the_workspace_position()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/resources").ToScope(AgentChatContextScopeId.Create()));

        var exception = await Assert.ThrowsAsync<AgentChatContextPositionUnavailableException>(
            async () => await registry.CaptureAsync());

        Assert.Equal(AgentChatContextPositionUnavailablePart.WorkspacePosition, exception.UnavailablePart);
        Assert.Equal("/resources", exception.KnownRoute);
    }

    [Fact]
    public async Task Strict_capture_rejects_a_workspace_position_without_a_surface_position()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("scheduler", "Scheduler", "/scheduler", "page"),
            AgentChatNavigationIdentity.Create());
        using var scopeLease = registry.ActivateScope(new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("scheduler"),
                new AgentChatContextSourceId("scheduler")),
            "Scheduler",
            workspaceScope: null,
            agentAccess: [],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            accessState: AgentChatContextAccessState.Ready));

        var exception = await Assert.ThrowsAsync<AgentChatContextPositionUnavailableException>(
            async () => await registry.CaptureAsync());

        Assert.Equal(AgentChatContextPositionUnavailablePart.SurfacePosition, exception.UnavailablePart);
        Assert.Equal("/scheduler", exception.KnownRoute);
    }

    [Fact]
    public async Task Strict_capture_recovers_only_after_the_surface_matches_the_new_workspace_route()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var initialNavigation = AgentChatNavigationIdentity.Create();
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("projects", "Projects", "/projects", "page"),
            initialNavigation);
        var scopeId = AgentChatContextScopeId.Create();
        using var scopeLease = registry.ActivateScope(CreateSurface("/projects").ToScope(scopeId));
        var first = Assert.IsType<AgentChatContextSnapshot>(await registry.CaptureAsync());

        var nextNavigation = AgentChatNavigationIdentity.Create();
        workspaceLease.Update(
            new AgentChatWorkspacePosition("crm", "CRM", "/crm-hr/crm", "page"),
            nextNavigation);
        await Assert.ThrowsAsync<AgentChatContextPositionMismatchException>(
            async () => await registry.CaptureAsync());

        scopeLease.Update(CreateSurface("/crm-hr/crm").ToScope(scopeId));
        scopeLease.SynchronizeNavigation(nextNavigation);
        var recovered = Assert.IsType<AgentChatContextSnapshot>(await registry.CaptureAsync());

        Assert.True(recovered.Version > first.Version);
        Assert.Equal("/crm-hr/crm", recovered.WorkspacePosition?.Route);
        Assert.Equal("/crm-hr/crm", recovered.Scope.SurfacePosition?.Route);
    }

    [Fact]
    public async Task Strict_capture_blocks_same_route_navigation_until_the_surface_synchronizes()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("live", "Live processes", "/processes/live", "page"),
            AgentChatNavigationIdentity.Create());
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/processes/live").ToScope(AgentChatContextScopeId.Create()));
        Assert.NotNull(await registry.CaptureAsync());

        var nextNavigation = AgentChatNavigationIdentity.Create();
        workspaceLease.Update(
            new AgentChatWorkspacePosition("live", "Live processes", "/processes/live", "page"),
            nextNavigation);

        Assert.Null(registry.Capture());
        var exception = await Assert.ThrowsAsync<AgentChatContextPositionMismatchException>(
            async () => await registry.CaptureAsync());
        Assert.Equal(AgentChatContextPositionMismatchReason.NavigationChanged, exception.Reason);

        scopeLease.SynchronizeNavigation(nextNavigation);

        Assert.NotNull(await registry.CaptureAsync());
    }

    [Fact]
    public void Navigation_identity_is_stable_for_query_order_and_changes_with_selection()
    {
        const string baseUri = "https://localhost/";
        var first = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://localhost/processes/live?runId=first&q=machines");
        var reordered = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://localhost/processes/live?q=machines&runId=first");
        var changed = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://localhost/processes/live?q=machines&runId=second");

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void Navigation_identity_canonicalizes_query_key_casing_and_module_overrides()
    {
        const string baseUri = "https://app.example/";
        var direct = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://app.example/crm-hr/directory?PartyId=party-42");
        var overridden = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://app.example/crm-hr/directory?PARTYID=stale",
            [new("partyId", "party-42")]);

        Assert.Equal(direct, overridden);
    }

    [Fact]
    public void Navigation_identity_query_override_represents_the_parameter_snapshot()
    {
        const string baseUri = "https://localhost/";
        const string currentLocation = "https://localhost/processes/live?q=machines&runId=second";
        var firstParameterSnapshot = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            currentLocation,
            [new("runId", "first")]);
        var firstLocation = AgentChatNavigationIdentity.CreateForLocation(
            baseUri,
            "https://localhost/processes/live?runId=first&q=machines");

        Assert.Equal(firstLocation, firstParameterSnapshot);
    }

    [Fact]
    public void Replaced_workspace_lease_cannot_clear_the_current_position()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/resources").ToScope(AgentChatContextScopeId.Create()));
        var replaced = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("old", "Old", "/old", "page"),
            AgentChatNavigationIdentity.Create());
        using var current = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("resources", "Resources", "/resources", "page"),
            AgentChatNavigationIdentity.Create());

        replaced.Dispose();

        Assert.Equal(
            "resources",
            Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).WorkspacePosition?.TabId);
    }

    [Fact]
    public async Task Async_capture_observes_cancellation_before_reading_context()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var scopeLease = registry.ActivateScope(
            CreateSurface("/scheduler").ToScope(AgentChatContextScopeId.Create()));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await registry.CaptureAsync(cancellation.Token));
    }

    [Fact]
    public void Position_models_reject_unbounded_or_unsafe_shape()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentChatSurfacePosition(
                "crm hr",
                "crm",
                "accounts",
                "/crm-hr/crm"));

        var selectedEntities = Enumerable.Range(
                0,
                AgentChatPositionLimits.MaximumSelectedEntities + 1)
            .Select(index => new AgentChatContextEntityReference(
                "node",
                index.ToString(),
                $"Node {index}"))
            .ToArray();
        Assert.Throws<ArgumentException>(() =>
            new AgentChatSurfacePosition(
                "projects",
                "structure",
                "canvas",
                "/projects/1/structure",
                selectedEntities: selectedEntities));
    }

    private static AgentChatContextSurface CreateSurface(
        string route,
        AgentChatContextEntityReference? selection = null)
    {
        return new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind("projects"),
                new AgentChatContextSourceId("projects")),
            "Projects",
            new AgentChatSurfacePosition(
                "projects",
                "portfolio",
                "projects",
                route,
                selection),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
    }
}

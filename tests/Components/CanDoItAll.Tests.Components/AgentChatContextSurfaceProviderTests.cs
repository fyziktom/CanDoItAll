using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentChatContextSurfaceProviderTests
{
    [Fact]
    public void Provider_updates_one_scope_replaces_sources_and_releases_all_leases()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        var initialSurface = CreateSurface("projects", "portfolio", "cards", "/projects");
        var initialFragment = CreateFragment("projects.filters", "Search: none");

        var cut = context.RenderComponent<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, initialSurface)
            .Add(component => component.Fragments, [initialFragment]));

        var initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal("cards", initial.Scope.SurfacePosition?.View);
        Assert.Equal("Search: none", Assert.Single(initial.Fragments).Content);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "projects",
                "portfolio",
                "cards",
                "/projects"))
            .Add(component => component.Fragments,
                [CreateFragment("projects.filters", "Search: none")]));

        Assert.Equal(initial.Version, registry.Capture()?.Version);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "projects",
                "portfolio",
                "files",
                "/projects"))
            .Add(component => component.Fragments,
                [CreateFragment("projects.filters", "Search: machine")]));

        var updated = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(initial.Scope.Id, updated.Scope.Id);
        Assert.True(updated.Version > initial.Version);
        Assert.Equal("files", updated.Scope.SurfacePosition?.View);
        Assert.Equal("Search: machine", Assert.Single(updated.Fragments).Content);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "resources",
                "resources",
                "registry",
                "/resources"))
            .Add(component => component.Fragments, Array.Empty<AgentChatContextFragment>()));

        var replaced = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.NotEqual(initial.Scope.Id, replaced.Scope.Id);
        Assert.Equal("resources", replaced.Scope.Source.Kind.Value);
        Assert.Empty(replaced.Fragments);

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
    }

    [Fact]
    public void Provider_rejects_duplicate_contributor_ids()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IAgentChatContextRegistry>(
            new AgentChatContextRegistry(TimeProvider.System));
        var fragments = new[]
        {
            CreateFragment("duplicate", "First"),
            CreateFragment("duplicate", "Second")
        };

        Assert.Throws<InvalidOperationException>(() =>
            context.RenderComponent<AgentChatContextSurfaceProvider>(parameters => parameters
                .Add(component => component.Surface, CreateSurface(
                    "scheduler",
                    "scheduler",
                    "calendar",
                    "/scheduler"))
                .Add(component => component.Fragments, fragments)));
    }

    [Fact]
    public async Task Access_state_override_blocks_capture_during_transition_and_recovers_same_scope()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        var surface = CreateSurface("resources", "resources", "registry", "/resources");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/resources");
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition(
                "route:resources",
                "Resources",
                "/resources",
                "page"),
            AgentChatNavigationIdentity.CreateForLocation(
                navigation.BaseUri,
                navigation.Uri));

        var cut = context.RenderComponent<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Loading));

        var loading = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(AgentChatContextAccessState.Loading, loading.Scope.AccessState);
        var unavailable = await Assert.ThrowsAsync<AgentChatContextUnavailableException>(
            async () => await registry.CaptureAsync());
        Assert.Equal(AgentChatContextAccessState.Loading, unavailable.AccessState);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        var ready = Assert.IsType<AgentChatContextSnapshot>(await registry.CaptureAsync());
        Assert.Equal(loading.Scope.Id, ready.Scope.Id);
        Assert.Equal(AgentChatContextAccessState.Ready, ready.Scope.AccessState);
    }

    private static AgentChatContextSurface CreateSurface(
        string sourceKind,
        string surface,
        string view,
        string route)
    {
        return new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind(sourceKind),
                new AgentChatContextSourceId(sourceKind)),
            $"{sourceKind} workspace",
            new AgentChatSurfacePosition(
                sourceKind,
                surface,
                view,
                route));
    }

    private static AgentChatContextFragment CreateFragment(
        string contributorId,
        string content)
        => new(
            new AgentChatContextContributorId(contributorId),
            order: 100,
            content);
}

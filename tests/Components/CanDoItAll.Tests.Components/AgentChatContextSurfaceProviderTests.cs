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
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        var initialSurface = CreateSurface("projects", "portfolio", "cards", "/projects");
        var initialFragment = CreateFragment("projects.filters", "Search: none");

        var cut = context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, initialSurface)
            .Add(component => component.Fragments, [initialFragment]));

        var initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal("cards", initial.Scope.SurfacePosition?.View);
        Assert.Equal("Search: none", Assert.Single(initial.Fragments).Content);

        cut.Render(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "projects",
                "portfolio",
                "cards",
                "/projects"))
            .Add(component => component.Fragments,
                [CreateFragment("projects.filters", "Search: none")]));

        Assert.Equal(initial.Version, registry.Capture()?.Version);

        cut.Render(parameters => parameters
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

        cut.Render(parameters => parameters
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
        using var context = new BunitContext();
        context.Services.AddSingleton<IAgentChatContextRegistry>(
            new AgentChatContextRegistry(TimeProvider.System));
        var fragments = new[]
        {
            CreateFragment("duplicate", "First"),
            CreateFragment("duplicate", "Second")
        };

        Assert.Throws<InvalidOperationException>(() =>
            context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
                .Add(component => component.Surface, CreateSurface(
                    "scheduler",
                    "scheduler",
                    "calendar",
                    "/scheduler"))
                .Add(component => component.Fragments, fragments)));
    }

    [Fact]
    public void Provider_publishes_and_updates_atomic_contributor_publications()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        var initialAttachment = new SurfaceContextAttachment("initial");

        var cut = context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "processes",
                "processes",
                "board",
                "/processes"))
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.board",
                    "State: initial",
                    "content-initial",
                    initialAttachment)]));

        var initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var initialEnvelope = Assert.Single(initial.Attachments);
        Assert.Equal("State: initial", Assert.Single(initial.Fragments).Content);
        Assert.Equal(1, initialEnvelope.PublicationRevision.Value);
        Assert.True(
            initialEnvelope.TryGetAttachment<SurfaceContextAttachment>(
                out var capturedInitialAttachment));
        Assert.Same(initialAttachment, capturedInitialAttachment);

        var updatedAttachment = new SurfaceContextAttachment("updated");
        cut.Render(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "processes",
                "processes",
                "detail",
                "/processes"))
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.board",
                    "State: updated",
                    "content-updated",
                    updatedAttachment)]));

        var updated = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var updatedEnvelope = Assert.Single(updated.Attachments);
        Assert.Equal(initial.Scope.Id, updated.Scope.Id);
        Assert.Equal("detail", updated.Scope.SurfacePosition?.View);
        Assert.Equal("State: updated", Assert.Single(updated.Fragments).Content);
        Assert.Equal(2, updatedEnvelope.PublicationRevision.Value);
        Assert.True(
            updatedEnvelope.TryGetAttachment<SurfaceContextAttachment>(
                out var capturedUpdatedAttachment));
        Assert.Same(updatedAttachment, capturedUpdatedAttachment);

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
    }

    [Fact]
    public void Provider_rejects_mixed_atomic_publications_and_legacy_fragments()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IAgentChatContextRegistry>(
            new AgentChatContextRegistry(TimeProvider.System));

        Assert.Throws<InvalidOperationException>(() =>
            context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
                .Add(component => component.Surface, CreateSurface(
                    "processes",
                    "processes",
                    "board",
                    "/processes"))
                .Add(
                    component => component.Fragments,
                    [CreateFragment("processes.legacy", "Legacy")])
                .Add(
                    component => component.ContributorPublications,
                    [CreateContributorPublication(
                        "processes.atomic",
                        "Atomic",
                        "content-atomic",
                        new SurfaceContextAttachment("atomic"))])));
    }

    [Fact]
    public void Provider_transitions_between_legacy_and_atomic_publication_ownership()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);

        var cut = context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "processes",
                "processes",
                "board",
                "/processes"))
            .Add(
                component => component.Fragments,
                [CreateFragment("processes.legacy", "Legacy: initial")]));

        var legacy = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.True(legacy.Attachments.IsEmpty);

        cut.Render(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "processes",
                "processes",
                "board",
                "/processes"))
            .Add(
                component => component.Fragments,
                Array.Empty<AgentChatContextFragment>())
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.atomic",
                    "Atomic",
                    "content-atomic",
                    new SurfaceContextAttachment("atomic"))]));

        var atomic = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.NotEqual(legacy.Scope.Id, atomic.Scope.Id);
        Assert.Equal("Atomic", Assert.Single(atomic.Fragments).Content);
        Assert.Single(atomic.Attachments);

        cut.Render(parameters => parameters
            .Add(component => component.Surface, CreateSurface(
                "processes",
                "processes",
                "board",
                "/processes"))
            .Add(
                component => component.ContributorPublications,
                (IReadOnlyList<AgentChatContextContributorPublication>?)null)
            .Add(
                component => component.Fragments,
                [CreateFragment("processes.legacy", "Legacy: restored")]));

        var restoredLegacy = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.NotEqual(atomic.Scope.Id, restoredLegacy.Scope.Id);
        Assert.Equal(
            "Legacy: restored",
            Assert.Single(restoredLegacy.Fragments).Content);
        Assert.True(restoredLegacy.Attachments.IsEmpty);

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
    }

    [Fact]
    public void Provider_uses_attachment_fingerprints_and_stamps_as_atomic_equivalence_contract()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        var surface = CreateSurface(
            "processes",
            "processes",
            "board",
            "/processes");
        var initialAttachment = new SurfaceContextAttachment("initial");

        var cut = context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.atomic",
                    "Atomic",
                    "content-stable",
                    initialAttachment)]));

        var initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var equivalentPayload = new SurfaceContextAttachment("different-instance");
        cut.Render(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.atomic",
                    "Atomic",
                    "content-stable",
                    equivalentPayload)]));

        var equivalent = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var equivalentEnvelope = Assert.Single(equivalent.Attachments);
        Assert.Equal(initial.Version, equivalent.Version);
        Assert.Equal(1, equivalentEnvelope.PublicationRevision.Value);
        Assert.True(
            equivalentEnvelope.TryGetAttachment<SurfaceContextAttachment>(
                out var retainedAttachment));
        Assert.Same(initialAttachment, retainedAttachment);

        var laterCapture = new DateTimeOffset(
            2026,
            7,
            27,
            12,
            0,
            1,
            TimeSpan.Zero);
        cut.Render(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(
                component => component.ContributorPublications,
                [CreateContributorPublication(
                    "processes.atomic",
                    "Atomic",
                    "content-stable",
                    equivalentPayload,
                    laterCapture)]));

        var restamped = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.True(restamped.Version > equivalent.Version);
        Assert.Equal(
            2,
            Assert.Single(restamped.Attachments).PublicationRevision.Value);
    }

    [Fact]
    public async Task Access_state_override_blocks_capture_during_transition_and_recovers_same_scope()
    {
        using var context = new BunitContext();
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

        var cut = context.Render<AgentChatContextSurfaceProvider>(parameters => parameters
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Loading));

        var loading = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(AgentChatContextAccessState.Loading, loading.Scope.AccessState);
        var unavailable = await Assert.ThrowsAsync<AgentChatContextUnavailableException>(
            async () => await registry.CaptureAsync());
        Assert.Equal(AgentChatContextAccessState.Loading, unavailable.AccessState);

        cut.Render(parameters => parameters
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

    private static AgentChatContextContributorPublication CreateContributorPublication(
        string contributorId,
        string content,
        string contentFingerprint,
        SurfaceContextAttachment attachment,
        DateTimeOffset? capturedAtUtc = null)
    {
        var capturedAt = capturedAtUtc ?? new DateTimeOffset(
            2026,
            7,
            27,
            12,
            0,
            0,
            TimeSpan.Zero);
        return new AgentChatContextContributorPublication(
            CreateFragment(contributorId, content),
            [
                new AgentChatContextAttachmentDraft(
                    new AgentChatContextAttachmentKind("processes.snapshot"),
                    new SnapshotContentFingerprint(contentFingerprint),
                    new SnapshotCoverageFingerprint("processes-board"),
                    new DatabaseProfileGeneration(4),
                    new SnapshotFreshnessFingerprint("freshness-4"),
                    capturedAt,
                    capturedAt.AddMinutes(5),
                    attachment)
            ]);
    }

    private sealed record SurfaceContextAttachment(string Value) :
        IAgentChatContextAttachment;
}

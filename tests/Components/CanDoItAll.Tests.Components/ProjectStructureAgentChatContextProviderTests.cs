using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAgentChatContextProviderTests
{
    private static readonly DateTimeOffset InitialUtc =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Provider_recaptures_held_module_state_at_the_attachment_deadline()
    {
        using var context = new BunitContext();
        var clock = new ManualTimerTimeProvider(InitialUtc);
        var registry = new AgentChatContextRegistry(clock);
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(
            projectId,
            "Delivery project",
            [CreateNode("project:root", "Root")]);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(
            new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton<TimeProvider>(clock);

        var cut = context.Render<
            ProjectStructureAgentChatContextProvider>(parameters => parameters
                .Add(component => component.ProjectId, projectId)
                .Add(component => component.ProjectName, "Delivery project")
                .Add(component => component.Surface, surface)
                .Add(
                    component => component.ContextAccessState,
                    AgentChatContextAccessState.Ready));
        cut.WaitForAssertion(() =>
        {
            var initial = Assert.IsType<AgentChatContextSnapshot>(
                registry.Capture());
            Assert.Equal(InitialUtc, Assert.Single(initial.Attachments).CapturedAtUtc);
            Assert.Equal(1, clock.PendingTimerCount);
        });
        var original = Assert.IsType<AgentChatContextSnapshot>(
            registry.Capture());
        var originalAttachment = Assert.Single(original.Attachments);

        clock.Advance(
            ProjectStructureInvocationSnapshotMapper.FreshnessLifetime);

        cut.WaitForAssertion(() =>
        {
            var renewed = Assert.IsType<AgentChatContextSnapshot>(
                registry.Capture());
            var renewedAttachment = Assert.Single(renewed.Attachments);
            Assert.True(renewed.Version > original.Version);
            Assert.Equal(
                InitialUtc +
                ProjectStructureInvocationSnapshotMapper.FreshnessLifetime,
                renewedAttachment.CapturedAtUtc);
            Assert.Equal(
                originalAttachment.ContentFingerprint,
                renewedAttachment.ContentFingerprint);
            Assert.Equal(
                AgentChatContextAttachmentFreshness.Current,
                renewedAttachment.ResolveFreshness(
                    new DatabaseProfileGeneration(0),
                    clock.GetUtcNow()));
            Assert.Equal(1, clock.PendingTimerCount);
        });
    }

    [Fact]
    public async Task Provider_does_not_publish_parent_ready_while_agent_access_reload_is_loading_or_failed()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        var referenceDataProvider = new ControllableAgentReferenceDataProvider(agent);
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(referenceDataProvider);
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(projectId, "Delivery project");

        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Single(snapshot.Attachments);
        });

        cacheInvalidator.Invalidate();

        cut.WaitForAssertion(() =>
        {
            Assert.True(referenceDataProvider.HasPendingReload);
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
            Assert.Empty(snapshot.Attachments);
            Assert.Throws<AgentChatContextUnavailableException>(() =>
                AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });

        var failedStatePublished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        registry.Changed += HandleRegistryChanged;
        try
        {
            referenceDataProvider.FailReload(new InvalidOperationException("Reference data reload failed."));
            await failedStatePublished.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Empty(snapshot.Attachments);
            Assert.Throws<AgentChatContextUnavailableException>(() =>
                AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        }
        finally
        {
            registry.Changed -= HandleRegistryChanged;
        }

        void HandleRegistryChanged(object? sender, EventArgs eventArgs)
        {
            if (registry.Capture()?.Scope.AccessState == AgentChatContextAccessState.Failed)
            {
                failedStatePublished.TrySetResult();
            }
        }
    }

    [Fact]
    public void Provider_blocks_parent_transition_context_and_recovers_on_the_same_project_scope()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(projectId, "Delivery project");

        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Loading));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
            Assert.Empty(snapshot.Attachments);
            Assert.Throws<AgentChatContextUnavailableException>(() =>
                AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
        var loadingSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(loadingSnapshot.Scope.Id, snapshot.Scope.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Single(snapshot.Attachments);
            Assert.NotNull(AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
    }

    [Fact]
    public void Provider_replaces_canvas_and_gantt_fragments_without_leaking_context()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var notificationHub = new RecordingNotificationHub();
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        ProjectStructureNode[] selectedNodes =
        [
            CreateNode("node:alpha", "Alpha node"),
            CreateNode("node:beta", "Beta node")
        ];
        var surface = CreateSurface(
            projectId,
            "Delivery project",
            selectedNodes);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(notificationHub);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);

        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ActiveView, ProjectStructureAgentChatView.Canvas)
            .Add(component => component.SelectedNodes, selectedNodes));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            AssertProjectSource(snapshot, projectId);
            AssertContributors(snapshot);
            Assert.Single(snapshot.Attachments);
            Assert.True(snapshot.CanRead(agent.Id));
            Assert.Contains(
                "Current project workspace view: structure canvas.",
                FindFragment(snapshot, ProjectStructureAgentChatContextBuilder.ViewContributorId).Content,
                StringComparison.Ordinal);
            Assert.Contains(
                "Selected project-structure node: node:alpha | Alpha node.",
                FindFragment(snapshot, ProjectStructureAgentChatContextBuilder.SelectionContributorId).Content,
                StringComparison.Ordinal);
        });

        var canvasSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var scopeId = canvasSnapshot.Scope.Id;
        var source = canvasSnapshot.Scope.Source;

        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ActiveView, ProjectStructureAgentChatView.Gantt)
            .Add(component => component.SelectedNodes, Array.Empty<ProjectStructureNode>()));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(scopeId, snapshot.Scope.Id);
            Assert.Equal(source, snapshot.Scope.Source);
            Assert.True(snapshot.Version > canvasSnapshot.Version);
            AssertContributors(snapshot);
            var attachment = Assert.Single(snapshot.Attachments);
            Assert.True(
                attachment.TryGetAttachment<ProjectStructureInvocationSnapshot>(
                    out var invocationSnapshot));
            Assert.Equal(ProjectStructureAgentChatView.Gantt, invocationSnapshot.ActiveView);
            var viewFragment = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.ViewContributorId);
            Assert.Contains(
                "Current project workspace view: Gantt schedule.",
                viewFragment.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain("structure canvas", viewFragment.Content, StringComparison.Ordinal);
            var selectionFragment = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId);
            Assert.Contains(
                "Selected project-structure nodes: none.",
                selectionFragment.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain("node:alpha", selectionFragment.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("node:beta", selectionFragment.Content, StringComparison.Ordinal);
        });

        var ganttSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.Surface, surface)
            .Add(component => component.ActiveView, ProjectStructureAgentChatView.Canvas)
            .Add(component => component.SelectedNodes, selectedNodes));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(scopeId, snapshot.Scope.Id);
            Assert.Equal(source, snapshot.Scope.Source);
            Assert.True(snapshot.Version > ganttSnapshot.Version);
            AssertContributors(snapshot);
            var viewFragment = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.ViewContributorId);
            Assert.Contains(
                "Current project workspace view: structure canvas.",
                viewFragment.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Gantt schedule", viewFragment.Content, StringComparison.Ordinal);
            var selectionFragment = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId);
            Assert.Contains(
                "Selected project-structure node: node:alpha | Alpha node.",
                selectionFragment.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Selected project-structure nodes: none.",
                selectionFragment.Content,
                StringComparison.Ordinal);
        });

        Assert.Equal(source, Assert.Single(notificationHub.ActiveSources));
        Assert.Equal(1, cacheInvalidator.SubscriptionCount);

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
        Assert.Empty(notificationHub.ActiveSources);
        Assert.Equal(0, cacheInvalidator.SubscriptionCount);
    }

    [Fact]
    public void Provider_publishes_bounded_typed_selection_details_and_refreshes_when_node_data_changes()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        var selectedNode = CreateNode("custom:architecture", "Main Architecture") with
        {
            ParentId = $"project:{projectId:N}",
            ObjectType = ProjectObjectType.ProjectBlock,
            ObjectSubtype = "architecture",
            ArtifactKind = "ProjectBlock",
            Status = "Draft",
            Notes = "Initial architecture summary.",
            MetadataJson = """{"owner":"architecture"}""",
            MediaRelativePath = "external-target/C/example/architecture.md",
            MediaContentType = "text/markdown",
            MediaOriginalFileName = "architecture.md"
        };
        var projectRoot = CreateNode(
            selectedNode.ParentId!,
            "Architecture project");
        var surface = CreateSurface(
            projectId,
            "Architecture project",
            [projectRoot, selectedNode]);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);

        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Architecture project")
            .Add(component => component.Surface, surface)
            .Add(component => component.SelectedNodes, new[] { selectedNode }));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var baseContent = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.BaseContributorId).Content;
            Assert.Contains("request.nodeIds is exact-node-only", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.subtreeRootIds", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.includeLinks", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.includeNotes", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.includeMetadata", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.includeAssets", baseContent, StringComparison.Ordinal);
            Assert.Contains("ContextDefault", baseContent, StringComparison.Ordinal);
            Assert.Contains("InvocationSnapshot", baseContent, StringComparison.Ordinal);
            Assert.Contains("CanonicalCurrent", baseContent, StringComparison.Ordinal);
            Assert.Contains("request.sourceWorkspacePath", baseContent, StringComparison.Ordinal);
            Assert.Contains("no external-target path is needed", baseContent, StringComparison.Ordinal);
            Assert.Contains("project_structure_asset_get", baseContent, StringComparison.Ordinal);
            Assert.Contains("project_structure_asset_content_get", baseContent, StringComparison.Ordinal);
            Assert.Contains("do not grant workspace access", baseContent, StringComparison.Ordinal);

            var selectionContent = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId).Content;
            Assert.Contains("objectType: ProjectBlock", selectionContent, StringComparison.Ordinal);
            Assert.Contains("objectSubtype: architecture", selectionContent, StringComparison.Ordinal);
            Assert.Contains("status: Draft", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Initial architecture summary.", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("metadataJson", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("external-target", selectionContent, StringComparison.Ordinal);
            Assert.Contains(
                "parentNodeKey=\"custom:architecture\"",
                selectionContent,
                StringComparison.Ordinal);
            Assert.True(selectionContent.Length < AgentChatContextFragment.MaximumContentLength);

            var envelope = Assert.Single(snapshot.Attachments);
            Assert.Equal(
                ProjectStructureInvocationSnapshotMapper.AttachmentKindValue,
                envelope.Kind.Value);
            Assert.True(
                envelope.TryGetAttachment<ProjectStructureInvocationSnapshot>(
                    out var invocationSnapshot));
            var attachmentNode = Assert.Single(
                invocationSnapshot.Nodes,
                node => node.Id == selectedNode.Id);
            Assert.Equal("Draft", attachmentNode.Status);
            Assert.Equal(
                envelope.ContentFingerprint,
                ProjectStructureInvocationSnapshotMapper.ComputeContentFingerprint(
                    invocationSnapshot));
        });
        var originalSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var oversizedNotes = new string('x', 600);
        var updatedNode = selectedNode with
        {
            Status = "Ready",
            Notes = oversizedNotes,
            MetadataJson = """{"owner":"delivery"}"""
        };
        var updatedSurface = CreateSurface(
            projectId,
            "Architecture project",
            [projectRoot, updatedNode]);

        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Architecture project")
            .Add(component => component.Surface, updatedSurface)
            .Add(component => component.SelectedNodes, new[] { updatedNode }));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.True(snapshot.Version > originalSnapshot.Version);
            var selectionContent = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId).Content;
            Assert.Contains("status: Ready", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("metadataJson", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain(oversizedNotes, selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Initial architecture summary.", selectionContent, StringComparison.Ordinal);
            var envelope = Assert.Single(snapshot.Attachments);
            Assert.True(
                envelope.TryGetAttachment<ProjectStructureInvocationSnapshot>(
                    out var invocationSnapshot));
            Assert.Equal(
                "Ready",
                Assert.Single(
                    invocationSnapshot.Nodes,
                    node => node.Id == updatedNode.Id).Status);
            Assert.True(selectionContent.Length < AgentChatContextFragment.MaximumContentLength);
        });
    }

    [Fact]
    public void Provider_without_a_held_structure_surface_publishes_no_usable_snapshot()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(
            new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);

        context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.ProjectName, "Loading project")
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
        Assert.Empty(snapshot.Attachments);
        Assert.Throws<AgentChatContextUnavailableException>(() =>
            AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
    }

    [Fact]
    public void Provider_replaces_scope_fragments_and_snapshot_as_one_atomic_publication()
    {
        using var context = new BunitContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        var root = CreateNode("project:root", "Root");
        var oldNode = CreateNode("node:selected", "Old selection") with
        {
            ParentId = root.Id,
            Status = "Draft"
        };
        var newNode = oldNode with
        {
            Title = "New selection",
            Status = "Ready"
        };
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(
            new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        context.Services.AddSingleton<IDatabaseRuntimeState>(
            new DatabaseRuntimeState(new DatabaseSwitchNotificationService()));
        context.Services.AddSingleton(TimeProvider.System);

        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Old project")
            .Add(component => component.Surface, CreateSurface(
                projectId,
                "Old project",
                [root, oldNode]))
            .Add(component => component.SelectedNodes, new[] { oldNode }));
        var initialSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var observations = new List<string>();
        registry.Changed += HandleChanged;
        try
        {
            cut.Render(parameters => parameters
                .Add(component => component.ProjectId, projectId)
                .Add(component => component.ProjectName, "New project")
                .Add(component => component.Surface, CreateSurface(
                    projectId,
                    "New project",
                    [root, newNode]))
                .Add(component => component.SelectedNodes, new[] { newNode }));
        }
        finally
        {
            registry.Changed -= HandleChanged;
        }

        var finalSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.True(finalSnapshot.Version > initialSnapshot.Version);
        Assert.NotEmpty(observations);
        Assert.Equal("New project|New selection|Ready", observations[^1]);

        void HandleChanged(object? sender, EventArgs eventArgs)
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var envelope = Assert.Single(snapshot.Attachments);
            Assert.True(
                envelope.TryGetAttachment<ProjectStructureInvocationSnapshot>(
                    out var invocationSnapshot));
            var selectedNode = Assert.Single(
                invocationSnapshot.Nodes,
                node => node.Id == "node:selected");
            var selectionFragment = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId);
            Assert.Contains(invocationSnapshot.ProjectName, snapshot.Scope.DisplayName, StringComparison.Ordinal);
            Assert.Contains(selectedNode.Title, selectionFragment.Content, StringComparison.Ordinal);
            Assert.Contains($"status: {selectedNode.Status}", selectionFragment.Content, StringComparison.Ordinal);
            observations.Add(
                $"{invocationSnapshot.ProjectName}|{selectedNode.Title}|{selectedNode.Status}");
        }
    }

    private static void AssertProjectSource(
        AgentChatContextSnapshot snapshot,
        Guid projectId)
    {
        Assert.Equal(
            ProjectStructureAgentChatContextBuilder.SourceKind,
            snapshot.Scope.Source.Kind.Value);
        Assert.Equal(projectId.ToString("D"), snapshot.Scope.Source.Id.Value);
    }

    private static void AssertContributors(AgentChatContextSnapshot snapshot)
    {
        Assert.Collection(
            snapshot.Fragments,
            fragment => Assert.Equal(
                ProjectStructureAgentChatContextBuilder.BaseContributorId,
                fragment.ContributorId.Value),
            fragment => Assert.Equal(
                ProjectStructureAgentChatContextBuilder.ViewContributorId,
                fragment.ContributorId.Value),
            fragment => Assert.Equal(
                ProjectStructureAgentChatContextBuilder.SelectionContributorId,
                fragment.ContributorId.Value));
    }

    private static AgentChatContextFragment FindFragment(
        AgentChatContextSnapshot snapshot,
        string contributorId)
    {
        return Assert.Single(snapshot.Fragments, fragment =>
            string.Equals(
                fragment.ContributorId.Value,
                contributorId,
                StringComparison.Ordinal));
    }

    private static AgentDefinition CreateAgent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            null,
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                AllowAllProjects = true
            });
        return new AgentDefinition(
            Guid.NewGuid(),
            "Project assistant",
            "Assistant",
            "Project structure test agent",
            "Help with the selected project.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp);
    }

    private static ProjectStructureSurface CreateSurface(
        Guid projectId,
        string projectName,
        IReadOnlyList<ProjectStructureNode>? nodes = null)
    {
        return new ProjectStructureSurface(
            projectId,
            projectName,
            nodes ?? [],
            [],
            null);
    }

    private static ProjectStructureNode CreateNode(string id, string title)
    {
        return new ProjectStructureNode(
            id,
            null,
            ProjectObjectType.Note,
            "note",
            title,
            string.Empty,
            "Draft",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("pill", "#64748b", "NT", "Note"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
    }

    private sealed class StubAgentReferenceDataProvider(
        AgentDefinition agent) : IAgentReferenceDataProvider
    {
        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [agent],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero));
        }
    }

    private sealed class ControllableAgentReferenceDataProvider(
        AgentDefinition agent) : IAgentReferenceDataProvider
    {
        private readonly AgentReferenceDataSnapshot snapshot = new(
            AgentReferenceDataSections.Agents,
            [agent],
            [],
            new Dictionary<Guid, ProviderProfile>(),
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);
        private TaskCompletionSource<AgentReferenceDataSnapshot>? pendingReload;
        private int callCount;

        public bool HasPendingReload => Volatile.Read(ref pendingReload) is not null;

        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref callCount) == 1)
            {
                return Task.FromResult(snapshot);
            }

            var completion = new TaskCompletionSource<AgentReferenceDataSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref pendingReload, completion);
            return completion.Task.WaitAsync(cancellationToken);
        }

        public void FailReload(Exception exception)
        {
            var completion = Interlocked.Exchange(ref pendingReload, null);
            Assert.NotNull(completion);
            completion.SetException(exception);
        }
    }

    private sealed class RecordingReferenceDataCacheInvalidator : IAgentReferenceDataCacheInvalidator
    {
        private EventHandler? invalidated;

        public event EventHandler? Invalidated
        {
            add => invalidated += value;
            remove => invalidated -= value;
        }

        public int SubscriptionCount => invalidated?.GetInvocationList().Length ?? 0;

        public void Invalidate()
            => invalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingNotificationHub : IAgentChatExecutionNotificationHub
    {
        private readonly Dictionary<Guid, AgentChatContextSource> subscriptions = [];

        public IReadOnlyCollection<AgentChatContextSource> ActiveSources
            => subscriptions.Values.ToArray();

        public IAgentChatExecutionNotificationSubscription Subscribe(
            AgentChatContextSource source,
            Func<AgentChatExecutionCompleted, Task> handler)
        {
            var id = Guid.NewGuid();
            subscriptions.Add(id, source);
            return new Subscription(this, id, source);
        }

        public Task PublishAsync(
            AgentChatExecutionCompleted notification,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private sealed class Subscription(
            RecordingNotificationHub owner,
            Guid id,
            AgentChatContextSource source) : IAgentChatExecutionNotificationSubscription
        {
            private RecordingNotificationHub? owner = owner;

            public AgentChatContextSource Source { get; } = source;

            public void Dispose()
            {
                var currentOwner = Interlocked.Exchange(ref owner, null);
                currentOwner?.subscriptions.Remove(id);
            }
        }
    }

    private sealed class ManualTimerTimeProvider(
        DateTimeOffset initialUtc) : TimeProvider
    {
        private readonly object sync = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = initialUtc;

        public int PendingTimerCount
        {
            get
            {
                lock (sync)
                {
                    return timers.Count;
                }
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (sync)
            {
                return utcNow;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);

            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(
                duration,
                TimeSpan.Zero);

            ManualTimer[] dueTimers;
            lock (sync)
            {
                utcNow += duration;
                dueTimers = timers
                    .Where(timer => timer.IsDue(utcNow))
                    .ToArray();
                foreach (var timer in dueTimers)
                {
                    timer.MarkFired();
                    timers.Remove(timer);
                }
            }

            foreach (var timer in dueTimers)
            {
                timer.Invoke();
            }
        }

        private bool ChangeTimer(
            ManualTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }

            if (period != Timeout.InfiniteTimeSpan)
            {
                throw new NotSupportedException(
                    "The context test time provider supports one-shot timers only.");
            }

            lock (sync)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                timer.DueAtUtc = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : utcNow + dueTime;
                if (!timers.Contains(timer))
                {
                    timers.Add(timer);
                }

                return true;
            }
        }

        private void DisposeTimer(ManualTimer timer)
        {
            lock (sync)
            {
                timer.MarkDisposed();
                timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(
            ManualTimerTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private int isDisposed;

            public DateTimeOffset? DueAtUtc { get; set; }

            public bool IsDisposed
                => Volatile.Read(ref isDisposed) == 1;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return owner.ChangeTimer(this, dueTime, period);
            }

            public void Dispose()
            {
                owner.DisposeTimer(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public bool IsDue(DateTimeOffset nowUtc)
            {
                return !IsDisposed &&
                       DueAtUtc.HasValue &&
                       DueAtUtc.Value <= nowUtc;
            }

            public void MarkFired()
            {
                DueAtUtc = null;
            }

            public void MarkDisposed()
            {
                Interlocked.Exchange(ref isDisposed, 1);
                DueAtUtc = null;
            }

            public void Invoke()
            {
                if (!IsDisposed)
                {
                    callback(state);
                }
            }
        }
    }
}

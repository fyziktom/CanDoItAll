using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAgentChatContextProviderTests
{
    [Fact]
    public async Task Provider_does_not_publish_parent_ready_while_agent_access_reload_is_loading_or_failed()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        var referenceDataProvider = new ControllableAgentReferenceDataProvider(agent);
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(referenceDataProvider);
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);

        var cut = context.RenderComponent<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
        });

        cacheInvalidator.Invalidate();

        cut.WaitForAssertion(() =>
        {
            Assert.True(referenceDataProvider.HasPendingReload);
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
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
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());
        var projectId = Guid.NewGuid();

        var cut = context.RenderComponent<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Loading));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
            Assert.Throws<AgentChatContextUnavailableException>(() =>
                AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
        var loadingSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(loadingSnapshot.Scope.Id, snapshot.Scope.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.NotNull(AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
    }

    [Fact]
    public void Provider_replaces_canvas_and_gantt_fragments_without_leaking_context()
    {
        using var context = new TestContext();
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
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(notificationHub);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);

        var cut = context.RenderComponent<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.ActiveView, ProjectStructureAgentChatView.Canvas)
            .Add(component => component.SelectedNodes, selectedNodes));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            AssertProjectSource(snapshot, projectId);
            AssertContributors(snapshot);
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

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
            .Add(component => component.ActiveView, ProjectStructureAgentChatView.Gantt)
            .Add(component => component.SelectedNodes, Array.Empty<ProjectStructureNode>()));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(scopeId, snapshot.Scope.Id);
            Assert.Equal(source, snapshot.Scope.Source);
            Assert.True(snapshot.Version > canvasSnapshot.Version);
            AssertContributors(snapshot);
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

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Delivery project")
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
        using var context = new TestContext();
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
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());

        var cut = context.RenderComponent<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Architecture project")
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
            Assert.Contains("notes: Initial architecture summary.", selectionContent, StringComparison.Ordinal);
            Assert.Contains(
                "parentNodeKey=\"custom:architecture\"",
                selectionContent,
                StringComparison.Ordinal);
        });
        var originalSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var oversizedNotes = new string('x', 600);
        var updatedNode = selectedNode with
        {
            Status = "Ready",
            Notes = oversizedNotes,
            MetadataJson = """{"owner":"delivery"}"""
        };

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Architecture project")
            .Add(component => component.SelectedNodes, new[] { updatedNode }));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.True(snapshot.Version > originalSnapshot.Version);
            var selectionContent = FindFragment(
                snapshot,
                ProjectStructureAgentChatContextBuilder.SelectionContributorId).Content;
            Assert.Contains("status: Ready", selectionContent, StringComparison.Ordinal);
            Assert.Contains("""metadataJson: {"owner":"delivery"}""", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain(oversizedNotes, selectionContent, StringComparison.Ordinal);
            Assert.Contains(new string('x', 319) + "…", selectionContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Initial architecture summary.", selectionContent, StringComparison.Ordinal);
            Assert.True(selectionContent.Length < AgentChatContextFragment.MaximumContentLength);
        });
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
}

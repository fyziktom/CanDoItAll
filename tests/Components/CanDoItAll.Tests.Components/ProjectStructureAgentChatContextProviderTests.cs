using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAgentChatContextProviderTests
{
    [Fact]
    public void Provider_replaces_canvas_and_gantt_fragments_without_leaking_context()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var notificationHub = new RecordingNotificationHub();
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        string[] selectedNodeIds = ["node:alpha", "node:beta"];
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
            .Add(component => component.SelectedNodeIds, selectedNodeIds));

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
                "Selected project-structure node ids: node:alpha, node:beta.",
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
            .Add(component => component.SelectedNodeIds, Array.Empty<string>()));

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
                "Selected project-structure node ids: none.",
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
            .Add(component => component.SelectedNodeIds, selectedNodeIds));

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
                "Selected project-structure node ids: node:alpha, node:beta.",
                selectionFragment.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Selected project-structure node ids: none.",
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

using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectsAgentChatContextProviderTests
{
    [Fact]
    public async Task Provider_tracks_project_focus_refreshes_matching_source_and_releases_context()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var notificationHub = new RecordingNotificationHub();
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        var agent = CreateAgent();
        var root = CreateProject(Guid.NewGuid(), "Root project", childCount: 1);
        var child = CreateProject(Guid.NewGuid(), "Child project", parentCount: 1);
        var unrelated = CreateProject(Guid.NewGuid(), "Unrelated project");
        ProjectSummary[] projects = [root, child, unrelated];
        var refreshCount = 0;
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(notificationHub);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);

        var cut = context.RenderComponent<ProjectsAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectSummaries, projects)
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(ProjectsAgentChatContextBuilder.SourceKind, snapshot.Scope.Source.Kind.Value);
            Assert.Equal(ProjectsAgentChatContextBuilder.WorkspaceSourceId, snapshot.Scope.Source.Id.Value);
            Assert.True(snapshot.CanRead(agent.Id));
            var fragment = Assert.Single(snapshot.Fragments);
            Assert.Equal(ProjectsAgentChatContextBuilder.PortfolioContributorId, fragment.ContributorId.Value);
            Assert.Contains("SelectedProject: None", fragment.Content, StringComparison.Ordinal);
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectSummaries, projects)
            .Add(component => component.SelectedProjectId, root.Id)
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        AgentChatContextSnapshot rootSnapshot = null!;
        cut.WaitForAssertion(() =>
        {
            rootSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(root.Id.ToString("D"), rootSnapshot.Scope.Source.Id.Value);
            Assert.Equal(
                WorkspaceScopeDescriptor.Project(root.Id.ToString("D")),
                rootSnapshot.Scope.WorkspaceScope);
            Assert.True(rootSnapshot.CanRead(agent.Id));
            var selection = FindFragment(rootSnapshot, ProjectsAgentChatContextBuilder.SelectionContributorId);
            Assert.Contains($"SelectedProjectId: {root.Id:D}", selection.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(child.Id.ToString("D"), selection.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(child.Name, selection.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelated.Id.ToString("D"), selection.Content, StringComparison.Ordinal);
        });

        await notificationHub.PublishAsync(CreateCompletion(rootSnapshot, agent.Id));
        cut.WaitForAssertion(() => Assert.Equal(1, refreshCount));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectSummaries, projects)
            .Add(component => component.SelectedProjectId, child.Id)
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        AgentChatContextSnapshot childSnapshot = null!;
        cut.WaitForAssertion(() =>
        {
            childSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(child.Id.ToString("D"), childSnapshot.Scope.Source.Id.Value);
            var selection = FindFragment(childSnapshot, ProjectsAgentChatContextBuilder.SelectionContributorId);
            Assert.Contains($"SelectedProjectId: {child.Id:D}", selection.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(root.Id.ToString("D"), selection.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(root.Name, selection.Content, StringComparison.Ordinal);
        });

        await notificationHub.PublishAsync(CreateCompletion(rootSnapshot, agent.Id));
        Assert.Equal(1, refreshCount);
        await notificationHub.PublishAsync(CreateCompletion(childSnapshot, agent.Id));
        cut.WaitForAssertion(() => Assert.Equal(2, refreshCount));

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
        Assert.Equal(0, notificationHub.SubscriptionCount);
        Assert.Equal(0, cacheInvalidator.SubscriptionCount);
    }

    [Fact]
    public void Provider_does_not_disclose_other_projects_to_an_agent_restricted_to_the_selection()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var notificationHub = new RecordingNotificationHub();
        var cacheInvalidator = new RecordingReferenceDataCacheInvalidator();
        var selected = CreateProject(Guid.NewGuid(), "Allowed project", childCount: 1);
        var hiddenParent = CreateProject(Guid.NewGuid(), "Hidden parent");
        var hiddenChild = CreateProject(Guid.NewGuid(), "Hidden child", parentCount: 1);
        var restrictedAgent = CreateAgent([selected.Id]);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(notificationHub);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(restrictedAgent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);

        using var cut = context.RenderComponent<ProjectsAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectSummaries, new[] { selected, hiddenParent, hiddenChild })
            .Add(component => component.SelectedProjectId, selected.Id));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.True(snapshot.CanRead(restrictedAgent.Id));
            var combinedContext = string.Join('\n', snapshot.Fragments.Select(fragment => fragment.Content));
            Assert.Contains(selected.Id.ToString("D"), combinedContext, StringComparison.Ordinal);
            Assert.Contains(selected.Name, combinedContext, StringComparison.Ordinal);
            Assert.DoesNotContain(hiddenParent.Id.ToString("D"), combinedContext, StringComparison.Ordinal);
            Assert.DoesNotContain(hiddenParent.Name, combinedContext, StringComparison.Ordinal);
            Assert.DoesNotContain(hiddenChild.Id.ToString("D"), combinedContext, StringComparison.Ordinal);
            Assert.DoesNotContain(hiddenChild.Name, combinedContext, StringComparison.Ordinal);
            Assert.DoesNotContain("ProjectCount", combinedContext, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Provider_retries_agent_access_after_a_failed_load_when_project_focus_changes()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var project = CreateProject(Guid.NewGuid(), "Retry project");
        var agent = CreateAgent();
        var referenceDataProvider = new FailOnceAgentReferenceDataProvider(agent);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(referenceDataProvider);
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new RecordingReferenceDataCacheInvalidator());

        var cut = context.RenderComponent<ProjectsAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectSummaries, new[] { project }));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Equal(1, referenceDataProvider.CallCount);
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectSummaries, new[] { project })
            .Add(component => component.SelectedProjectId, project.Id));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.True(snapshot.CanRead(agent.Id));
            Assert.Equal(2, referenceDataProvider.CallCount);
        });
    }

    private static AgentChatExecutionCompleted CreateCompletion(
        AgentChatContextSnapshot snapshot,
        Guid agentId)
    {
        return new AgentChatExecutionCompleted(
            snapshot.Scope.Id,
            snapshot.Scope.Source,
            agentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    private static AgentChatContextFragment FindFragment(
        AgentChatContextSnapshot snapshot,
        string contributorId)
    {
        return Assert.Single(snapshot.Fragments, fragment =>
            string.Equals(fragment.ContributorId.Value, contributorId, StringComparison.Ordinal));
    }

    private static ProjectSummary CreateProject(
        Guid id,
        string name,
        int parentCount = 0,
        int childCount = 0)
    {
        return new ProjectSummary(
            id,
            name,
            ProjectStatus.Active,
            "Delivery",
            PhaseCount: 2,
            ParentCount: parentCount,
            ChildCount: childCount,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static AgentDefinition CreateAgent(IReadOnlyList<Guid>? allowedProjectIds = null)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            null,
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                AllowAllProjects = allowedProjectIds is null,
                AllowedProjectIds = allowedProjectIds?.ToList() ?? []
            });
        return new AgentDefinition(
            Guid.NewGuid(),
            "Projects assistant",
            "Assistant",
            "Projects test agent",
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

    private sealed class FailOnceAgentReferenceDataProvider(
        AgentDefinition agent) : IAgentReferenceDataProvider
    {
        public int CallCount { get; private set; }

        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (CallCount == 1)
            {
                throw new InvalidOperationException("Simulated reference-data failure.");
            }

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
        private readonly Dictionary<Guid, SubscriptionEntry> subscriptions = [];

        public int SubscriptionCount => subscriptions.Count;

        public IAgentChatExecutionNotificationSubscription Subscribe(
            AgentChatContextSource source,
            Func<AgentChatExecutionCompleted, Task> handler)
        {
            var id = Guid.NewGuid();
            subscriptions.Add(id, new SubscriptionEntry(source, handler));
            return new Subscription(this, id, source);
        }

        public async Task PublishAsync(
            AgentChatExecutionCompleted notification,
            CancellationToken cancellationToken = default)
        {
            foreach (var subscription in subscriptions.Values
                         .Where(entry => entry.Source == notification.Source)
                         .ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await subscription.Handler(notification);
            }
        }

        private sealed record SubscriptionEntry(
            AgentChatContextSource Source,
            Func<AgentChatExecutionCompleted, Task> Handler);

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

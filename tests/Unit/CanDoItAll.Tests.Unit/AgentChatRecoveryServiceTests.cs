using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentChatRecoveryServiceTests {
    [Fact]
    public async Task Separate_circuits_project_only_their_current_owner_conversations() {
        await using var first = new Fixture();
        await using var second = new Fixture(first.Catalog, first.Profile.Id, new(new string('b', 64)));
        var firstRun = first.AddExecution();
        var secondRun = second.AddExecution();

        await first.Service.InitializeAsync();
        await second.Service.InitializeAsync();

        Assert.Equal(firstRun.ChatSessionId, Assert.Single(first.Chats.Snapshot()).ChatSessionId);
        Assert.Equal(secondRun.ChatSessionId, Assert.Single(second.Chats.Snapshot()).ChatSessionId);
        Assert.NotEqual(firstRun.ChatSessionId, secondRun.ChatSessionId);
    }

    [Fact]
    public async Task Changing_owner_retires_old_projections_bindings_and_cached_agent_identity() {
        await using var fixture = new Fixture();
        var original = fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        fixture.Owner.Value = new(new string('b', 64));
        var replacement = fixture.AddExecution(agentId: original.AgentId, name: "Replacement owner's agent");

        await fixture.Service.InitializeAsync();

        var restored = Assert.Single(fixture.Chats.Snapshot());
        Assert.Equal(replacement.ChatSessionId, restored.ChatSessionId);
        Assert.Equal("Replacement owner's agent", restored.Agent.Name);
        Assert.Null(fixture.Contexts.TryGetBinding(AgentConversationKey.ForSession(original.ChatSessionId!.Value)));
        Assert.NotNull(fixture.Contexts.TryGetBinding(AgentConversationKey.ForSession(replacement.ChatSessionId!.Value)));
    }

    [Fact]
    public async Task Profile_change_during_an_agent_read_cannot_replay_the_old_partition() {
        await using var fixture = new Fixture();
        fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        fixture.AddExecution();
        var read = new TaskCompletionSource<IReadOnlyList<AgentDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.Pending = read.Task;
        var refresh = fixture.Service.InitializeAsync();
        Assert.False(refresh.IsCompleted);
        fixture.Profile.Generation = new(2);
        read.SetResult(fixture.Workspace.Agents);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => refresh);

        Assert.Empty(fixture.Chats.Snapshot());
    }

    [Fact]
    public async Task Revoked_owner_cannot_reopen_or_keep_old_recovery_content_visible() {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        fixture.Owner.Denied = true;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await fixture.Service.GetReturnLocationAsync(run.ConversationHandleId!.Value));

        Assert.Empty(fixture.Chats.Snapshot());
        Assert.Null(fixture.Contexts.TryGetBinding(AgentConversationKey.ForSession(run.ChatSessionId!.Value)));
    }

    [Fact]
    public async Task Missing_execution_removes_only_recovery_projections_without_stopping_local_drafts() {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        var draft = fixture.Chats.Open(new(Guid.NewGuid(), "Draft", null, null), null, FloatingAgentChatSettings.Default);
        fixture.Chats.SetDraftPending(draft.HandleId, true);
        fixture.Catalog.Entries.Clear();

        await fixture.Service.InitializeAsync();

        var retained = Assert.Single(fixture.Chats.Snapshot());
        Assert.Equal(draft.HandleId, retained.HandleId);
        Assert.True(retained.HasUnsentDraft);
        Assert.Null(fixture.Contexts.TryGetBinding(AgentConversationKey.ForSession(run.ChatSessionId!.Value)));
        Assert.Equal(0, fixture.Catalog.DismissCalls);
    }

    [Fact]
    public async Task Completion_is_published_once_per_operation_despite_repeated_catalog_updates() {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        var completed = run with {
            RunState = ActiveAgentChatRunState.Idle,
            CompletionNotification = new(AgentChatContextScopeId.Create(), new(new("project-structure"), new("source")),
                run.AgentId, run.ChatSessionId!.Value, run.ExecutionRunId!.Value, DateTimeOffset.UtcNow)
        };
        fixture.Catalog.Entries[0] = (fixture.Owner.Value, completed);

        await fixture.Service.InitializeAsync();
        fixture.Catalog.Entries[0] = (fixture.Owner.Value, completed with { Revision = completed.Revision + 1 });
        await fixture.Service.InitializeAsync();

        Assert.Single(fixture.Notifications.Published);
        Assert.Equal(completed.CompletionNotification, fixture.Notifications.Published[0]);
    }

    [Fact]
    public async Task Changed_owner_cannot_dismiss_the_previous_owners_execution() {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        fixture.Owner.Value = new(new string('b', 64));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.DismissAsync(run.ConversationHandleId!.Value));

        Assert.Equal(0, fixture.Catalog.DismissCalls);
        Assert.Single(fixture.Catalog.Entries);
        Assert.Empty(fixture.Chats.Snapshot());
    }

    [Fact]
    public async Task Reopening_returns_the_execution_location_and_does_not_navigate_or_submit() {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        await fixture.Service.InitializeAsync();

        var location = await fixture.Service.GetReturnLocationAsync(run.ConversationHandleId!.Value);

        Assert.Equal(run.ReturnLocation, location);
        Assert.Equal(1, fixture.Workspace.ReadCount);
        Assert.Equal(0, fixture.Catalog.DismissCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_and_profile_changes_retire_old_projections_without_a_catalog_event(bool changeProfile) {
        await using var fixture = new Fixture();
        var run = fixture.AddExecution();
        await fixture.Service.InitializeAsync();
        var retired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Chats.Changed += (_, _) => {
            if (fixture.Chats.Snapshot().Count == 0) {
                retired.TrySetResult();
            }
        };

        if (changeProfile) {
            fixture.Profile.Generation = new(2);
            fixture.ProfileNotifications.Publish(new(fixture.Profile.Id, "first", fixture.Profile.Id, "second", 2));
        } else {
            fixture.Owner.Value = new(new string('b', 64));
            fixture.Owner.RaiseChanged();
        }

        await retired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(fixture.Chats.Snapshot());
        await fixture.Service.InitializeAsync();
        Assert.Null(fixture.Contexts.TryGetBinding(AgentConversationKey.ForSession(run.ChatSessionId!.Value)));
    }

    private sealed class Fixture : IAsyncDisposable {
        public Fixture(Catalog? catalog = null, Guid? profileId = null, AgentChatOwnerId? owner = null) {
            Catalog = catalog ?? new();
            Profile = new(profileId ?? Guid.NewGuid());
            Owner.Value = owner ?? new(new string('a', 64));
            var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProxy>();
            Workspace = (WorkspaceProxy)(object)service;
            Service = new(Catalog, Owner, Profile, Profile, service, Chats, Contexts, Notifications, ProfileNotifications,
                NullLogger<AgentChatRecoveryService>.Instance);
        }

        public Catalog Catalog { get; }
        public MutableOwner Owner { get; } = new();
        public MutableProfile Profile { get; }
        public WorkspaceProxy Workspace { get; }
        public ActiveAgentChatRegistry Chats { get; } = new(TimeProvider.System);
        public AgentConversationContextService Contexts { get; } = new(TimeProvider.System);
        public Notifications Notifications { get; } = new();
        public DatabaseSwitchNotificationService ProfileNotifications { get; } = new();
        public AgentChatRecoveryService Service { get; }

        public AgentChatExecutionSnapshot AddExecution(Guid? agentId = null, string name = "Owned agent") {
            var agent = CreateAgent(agentId ?? Guid.NewGuid(), name);
            Workspace.Agents = [.. Workspace.Agents.Where(item => item.Id != agent.Id), agent];
            var session = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var binding = new AgentConversationContextBinding(AgentConversationContextMode.FollowCurrentSurface,
                AgentContextEpochId.Create(), new(2), now, now, chatSessionId: session,
                sourceKind: new("project-structure"), sourceId: new("source"), displayName: "Project");
            var snapshot = new AgentChatExecutionSnapshot(agent.Id, session, AgentChatHandleId.Create(),
                new(Profile.Id, WorkspaceScopeDescriptor.Sandbox, Profile.Generation, new(Guid.NewGuid())), Guid.NewGuid(),
                ActiveAgentChatRunState.Running, new("/projects/project/structure?tab=gantt"), now, Catalog.Entries.Count + 1, binding);
            Catalog.Entries.Add((Owner.Value, snapshot));
            return snapshot;
        }

        public ValueTask DisposeAsync() => Service.DisposeAsync();
    }

    private sealed class MutableOwner : IAgentChatOwnerProvider {
        public event EventHandler? Changed;
        public AgentChatOwnerId Value { get; set; }
        public bool Denied { get; set; }
        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
        public ValueTask<AgentChatOwnerId> GetOwnerAsync(CancellationToken cancellationToken = default)
            => Denied ? throw new UnauthorizedAccessException("Owner unavailable.") : ValueTask.FromResult(Value);
    }

    private sealed class MutableProfile(Guid id) : IDatabaseProfileRuntimeAccessor, IAgentExecutionProfileGenerationSource {
        public Guid Id { get; set; } = id;
        public DatabaseProfileGeneration Generation { get; set; } = new(1);
        public DatabaseProfileGeneration GetGeneration() => Generation;
        public ResolvedDatabaseProfile ResolveCurrentProfile() => new(new DatabaseProfileRecord {
            Id = Id, DisplayName = "Recovery test", ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory
        }, DatabaseProfileResolutionSource.ExplicitOverride, "test");
        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId) => requestedProfileId == Id
            ? ResolveCurrentProfile() : throw new KeyNotFoundException();
    }

    private sealed class Catalog : IAgentChatExecutionCatalog {
        public List<(AgentChatOwnerId Owner, AgentChatExecutionSnapshot Snapshot)> Entries { get; } = [];
        public int DismissCalls { get; private set; }
        public event EventHandler? Changed { add { } remove { } }
        public IReadOnlyList<AgentChatExecutionSnapshot> Snapshot(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation)
            => Entries.Where(entry => entry.Owner == owner && entry.Snapshot.StreamId.DatabaseProfileId == profileId &&
                    entry.Snapshot.StreamId.DatabaseProfileGeneration == generation).Select(entry => entry.Snapshot).ToArray();
        public bool TryDismiss(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation, Guid agentId, Guid chatSessionId) {
            DismissCalls++;
            return Entries.RemoveAll(entry => entry.Owner == owner && entry.Snapshot.AgentId == agentId &&
                entry.Snapshot.ChatSessionId == chatSessionId && entry.Snapshot.StreamId.DatabaseProfileId == profileId &&
                entry.Snapshot.StreamId.DatabaseProfileGeneration == generation) > 0;
        }
    }

    private sealed class Notifications : IAgentChatExecutionNotificationHub {
        public List<AgentChatExecutionCompleted> Published { get; } = [];
        public IAgentChatExecutionNotificationSubscription Subscribe(AgentChatContextSource source, Func<AgentChatExecutionCompleted, Task> handler)
            => throw new NotSupportedException();
        public Task PublishAsync(AgentChatExecutionCompleted notification, CancellationToken cancellationToken = default) {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    private class WorkspaceProxy : DispatchProxy {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
        public Task<IReadOnlyList<AgentDefinition>>? Pending { get; set; }
        public int ReadCount { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                throw new InvalidOperationException($"Unexpected workspace operation {targetMethod?.Name}.");
            }
            ReadCount++;
            return Pending ?? Task.FromResult(Agents);
        }
    }

    private static AgentDefinition CreateAgent(Guid id, string name) => new(id, name, "Assistant", "Recovery test", "Help.",
        AgentLifecycleStatus.Active, null, "test", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0.2,
        false, false, "{}", false, string.Empty, AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
